using TorreClou.Core.DTOs.Admin;
using TorreClou.Core.DTOs.Common;
using TorreClou.Core.DTOs.Financal;
using TorreClou.Core.DTOs.Payments;
using TorreClou.Core.Entities;
using TorreClou.Core.Entities.Financals;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Shared;
using TorreClou.Core.Specifications;

namespace TorreClou.Application.Services
{
    public class PaymentBusinessService(
        IUnitOfWork unitOfWork,
        IPaymentGateway paymentGateway, IWalletService walletService) : IPaymentBusinessService
    {
        public async Task<Result<string>> InitiateDepositAsync(int userId, decimal amount, string currency)
        {
            var user = await unitOfWork.Repository<User>().GetByIdAsync(userId);
            if (user == null)
            {
                return Result<string>.Failure("User not found.");
            }

            if (amount <= 0)
            {
                return Result<string>.Failure("Amount must be greater than zero.");
            }

            var deposit = new Deposit
            {
                UserId = userId,
                Amount = amount,
                Currency = currency,
                Status = DepositStatus.Pending,
                PaymentProvider = "Coinremitter"
            };
            unitOfWork.Repository<Deposit>().Add(deposit);
            await unitOfWork.Complete();


            var depositData = await paymentGateway.InitiatePaymentAsync(deposit, user);

         
            deposit.GatewayTransactionId = depositData.InvoiceId;
            deposit.PaymentUrl = depositData.PaymentUrl;
            unitOfWork.Repository<Deposit>().Update(deposit);
            await unitOfWork.Complete();

            return Result.Success(depositData.PaymentUrl);
        }

        public async Task<Result> ProcessCryptoWebhookAsync(string invoiceId, string coin)
        {
            Deposit? deposit = null;

            var spec = new BaseSpecification<Deposit>(d => d.GatewayTransactionId == invoiceId);
            deposit = await unitOfWork.Repository<Deposit>().GetEntityWithSpec(spec);

            if (deposit == null && int.TryParse(invoiceId, out var depositId))
            {
                deposit = await unitOfWork.Repository<Deposit>().GetByIdAsync(depositId);
            }

            if (deposit == null) return Result.Failure("Deposit not found");
            if (deposit.Status == DepositStatus.Completed) return Result.Success();

            var gatewayInvoiceId = deposit.GatewayTransactionId;
            var invoiceData = await paymentGateway.VerifyInvoiceAsync(gatewayInvoiceId!, coin);

            if (invoiceData == null) return Result.Failure("Could not verify payment with provider");

            if (invoiceData.status_code == 1 || invoiceData.status_code == 3)
            {
                deposit.Status = DepositStatus.Completed;

                var walletResult = await walletService.AddDepositAsync(
                    deposit.UserId,
                    deposit.Amount,
                    referenceId: invoiceData.invoice_id ?? deposit.Id.ToString(),
                    description: $"Crypto Deposit ({coin})"
                );

                if (walletResult.IsSuccess)
                {
                    deposit.WalletTransactionId = walletResult.Value;
                }
            }
            else if (invoiceData.status_code == 4 || invoiceData.status_code == 5)
            {
                deposit.Status = DepositStatus.Failed;
            }

            unitOfWork.Repository<Deposit>().Update(deposit);
            await unitOfWork.Complete();

            return Result.Success();
        }

        public async Task<Result<PaginatedResult<DepositDto>>> GetUserDepositsAsync(int userId, int pageNumber, int pageSize)
        {
            var spec = new UserDepositsSpecification(userId, pageNumber, pageSize);
            var countSpec = new BaseSpecification<Deposit>(x => x.UserId == userId);

            var deposits = await unitOfWork.Repository<Deposit>().ListAsync(spec);
            var totalCount = await unitOfWork.Repository<Deposit>().CountAsync(countSpec);

            var items = deposits.Select(d => new DepositDto
            {
                Id = d.Id,
                Amount = d.Amount,
                Currency = d.Currency,
                PaymentProvider = d.PaymentProvider,
                PaymentUrl = d.PaymentUrl,
                Status = d.Status.ToString(),
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList();

            return Result.Success(new PaginatedResult<DepositDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        public async Task<Result<DepositDto>> GetDepositByIdAsync(int userId, int depositId)
        {
            var spec = new BaseSpecification<Deposit>(x => x.Id == depositId && x.UserId == userId);
            var deposit = await unitOfWork.Repository<Deposit>().GetEntityWithSpec(spec);

            if (deposit == null)
            {
                return Result<DepositDto>.Failure("NOT_FOUND", "Deposit not found.");
            }

            return Result.Success(new DepositDto
            {
                Id = deposit.Id,
                Amount = deposit.Amount,
                Currency = deposit.Currency,
                PaymentProvider = deposit.PaymentProvider,
                PaymentUrl = deposit.PaymentUrl,
                Status = deposit.Status.ToString(),
                CreatedAt = deposit.CreatedAt,
                UpdatedAt = deposit.UpdatedAt
            });
        }

        public async Task<Result<PaginatedResult<AdminDepositDto>>> AdminGetAllDepositsAsync(int pageNumber, int pageSize, DepositStatus? status = null)
        {
            var spec = new AdminDepositsSpecification(pageNumber, pageSize, status);
            
            int totalCount;
            if (status.HasValue)
            {
                totalCount = await unitOfWork.Repository<Deposit>().CountAsync(x => x.Status == status.Value);
            }
            else
            {
                totalCount = await unitOfWork.Repository<Deposit>().CountAsync(x => true);
            }

            var deposits = await unitOfWork.Repository<Deposit>().ListAsync(spec);

            var items = deposits.Select(d => new AdminDepositDto
            {
                Id = d.Id,
                UserId = d.UserId,
                UserEmail = d.User?.Email ?? string.Empty,
                UserFullName = d.User?.FullName ?? string.Empty,
                Amount = d.Amount,
                Currency = d.Currency,
                PaymentProvider = d.PaymentProvider,
                GatewayTransactionId = d.GatewayTransactionId,
                Status = d.Status.ToString(),
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList();

            return Result.Success(new PaginatedResult<AdminDepositDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        public async Task<Result<AdminDashboardDto>> GetAnalyticsAsync(DateTime? dateFrom, DateTime? dateTo)
        {
            var from = dateFrom ?? DateTime.UtcNow.AddMonths(-1);
            var to = dateTo ?? DateTime.UtcNow;

            // Get all deposits in range
            var depositSpec = new BaseSpecification<Deposit>(d => d.CreatedAt >= from && d.CreatedAt <= to);
            var deposits = await unitOfWork.Repository<Deposit>().ListAsync(depositSpec);

            // Get all wallet transactions for total balance
            var allTransactions = await unitOfWork.Repository<WalletTransaction>().ListAllAsync();

            var dashboard = new AdminDashboardDto
            {
                TotalDepositsAmount = deposits.Where(d => d.Status == DepositStatus.Completed).Sum(d => d.Amount),
                TotalDepositsCount = deposits.Count,
                PendingDepositsCount = deposits.Count(d => d.Status == DepositStatus.Pending),
                CompletedDepositsCount = deposits.Count(d => d.Status == DepositStatus.Completed),
                FailedDepositsCount = deposits.Count(d => d.Status == DepositStatus.Failed),
                TotalWalletBalance = allTransactions.Sum(t => t.Amount),
                TotalUsersWithBalance = allTransactions.GroupBy(t => t.UserId).Count(g => g.Sum(t => t.Amount) > 0),
                DailyDeposits = GetDailyData(deposits, from, to),
                WeeklyDeposits = GetWeeklyData(deposits, from, to),
                MonthlyDeposits = GetMonthlyData(deposits, from, to)
            };

            return Result.Success(dashboard);
        }

        private static List<ChartDataPoint> GetDailyData(IReadOnlyList<Deposit> deposits, DateTime from, DateTime to)
        {
            var completedDeposits = deposits.Where(d => d.Status == DepositStatus.Completed).ToList();
            var result = new List<ChartDataPoint>();

            for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
            {
                var dayDeposits = completedDeposits.Where(d => d.CreatedAt.Date == date).ToList();
                result.Add(new ChartDataPoint
                {
                    Label = date.ToString("yyyy-MM-dd"),
                    Amount = dayDeposits.Sum(d => d.Amount),
                    Count = dayDeposits.Count
                });
            }

            return result;
        }

        private static List<ChartDataPoint> GetWeeklyData(IReadOnlyList<Deposit> deposits, DateTime from, DateTime to)
        {
            var completedDeposits = deposits.Where(d => d.Status == DepositStatus.Completed).ToList();
            var result = new List<ChartDataPoint>();

            var startOfWeek = from.Date.AddDays(-(int)from.DayOfWeek);
            while (startOfWeek <= to)
            {
                var endOfWeek = startOfWeek.AddDays(7);
                var weekDeposits = completedDeposits.Where(d => d.CreatedAt >= startOfWeek && d.CreatedAt < endOfWeek).ToList();
                
                result.Add(new ChartDataPoint
                {
                    Label = $"{startOfWeek:MMM dd} - {endOfWeek.AddDays(-1):MMM dd}",
                    Amount = weekDeposits.Sum(d => d.Amount),
                    Count = weekDeposits.Count
                });

                startOfWeek = endOfWeek;
            }

            return result;
        }

        private static List<ChartDataPoint> GetMonthlyData(IReadOnlyList<Deposit> deposits, DateTime from, DateTime to)
        {
            var completedDeposits = deposits.Where(d => d.Status == DepositStatus.Completed).ToList();
            var result = new List<ChartDataPoint>();

            var current = new DateTime(from.Year, from.Month, 1);
            while (current <= to)
            {
                var nextMonth = current.AddMonths(1);
                var monthDeposits = completedDeposits.Where(d => d.CreatedAt >= current && d.CreatedAt < nextMonth).ToList();
                
                result.Add(new ChartDataPoint
                {
                    Label = current.ToString("MMM yyyy"),
                    Amount = monthDeposits.Sum(d => d.Amount),
                    Count = monthDeposits.Count
                });

                current = nextMonth;
            }

            return result;
        }
    }
}
