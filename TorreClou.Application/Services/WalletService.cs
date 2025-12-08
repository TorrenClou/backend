using TorreClou.Core.DTOs.Admin;
using TorreClou.Core.DTOs.Common;
using TorreClou.Core.DTOs.Financal;
using TorreClou.Core.Entities;
using TorreClou.Core.Entities.Financals;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Shared;
using TorreClou.Core.Specifications;

namespace TorreClou.Application.Services
{
    public class WalletService(IUnitOfWork unitOfWork) : IWalletService
    {
        public async Task<Result<int>> AddDepositAsync(int userId, decimal amount, string? referenceId = null, string description = "Deposit")
        {
            if (amount <= 0)
            {
                return Result<int>.Failure("DEPOSIT_ERROR", "Deposit amount must be greater than zero.");
            }

            var user = await unitOfWork.Repository<User>().GetByIdAsync(userId);
            if (user == null)
            {
                return Result<int>.Failure("USER_ERROR", "User not found.");
            }

            var transaction = new WalletTransaction
            {
                UserId = userId,
                Amount = amount,
                Type = TransactionType.DEPOSIT,
                ReferenceId = referenceId,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            unitOfWork.Repository<WalletTransaction>().Add(transaction);

            var result = await unitOfWork.Complete();

            if (result <= 0)
                return Result<int>.Failure("DATABASE_ERROR", "Failed to save transaction to database.");

            return Result<int>.Success(transaction.Id);
        }

        public async Task<Result<decimal>> GetUserBalanceAsync(int userId)
        {
            var spec = new BaseSpecification<WalletTransaction>(x => x.UserId == userId);
            var transactions = await unitOfWork.Repository<WalletTransaction>().ListAsync(spec);

            return Result.Success(transactions.Sum(x => x.Amount));
        }

        public async Task<Result<PaginatedResult<WalletTransactionDto>>> GetUserTransactionsAsync(int userId, int pageNumber, int pageSize)
        {
            var spec = new UserTransactionsSpecification(userId, pageNumber, pageSize);
            var countSpec = new BaseSpecification<WalletTransaction>(x => x.UserId == userId);

            var transactions = await unitOfWork.Repository<WalletTransaction>().ListAsync(spec);
            var totalCount = await unitOfWork.Repository<WalletTransaction>().CountAsync(countSpec);

            var items = transactions.Select(t => new WalletTransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type.ToString(),
                ReferenceId = t.ReferenceId,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList();

            return Result.Success(new PaginatedResult<WalletTransactionDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        public async Task<Result<WalletTransactionDto>> GetTransactionByIdAsync(int userId, int transactionId)
        {
            var spec = new BaseSpecification<WalletTransaction>(x => x.Id == transactionId && x.UserId == userId);
            var transaction = await unitOfWork.Repository<WalletTransaction>().GetEntityWithSpec(spec);

            if (transaction == null)
            {
                return Result<WalletTransactionDto>.Failure("NOT_FOUND", "Transaction not found.");
            }

            return Result.Success(new WalletTransactionDto
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Type = transaction.Type.ToString(),
                ReferenceId = transaction.ReferenceId,
                Description = transaction.Description,
                CreatedAt = transaction.CreatedAt
            });
        }

        public async Task<Result<PaginatedResult<WalletTransactionDto>>> AdminGetAllTransactionsAsync(int pageNumber, int pageSize)
        {
            var spec = new AllTransactionsSpecification(pageNumber, pageSize);
            var totalCount = await unitOfWork.Repository<WalletTransaction>().CountAsync(x => true);

            var transactions = await unitOfWork.Repository<WalletTransaction>().ListAsync(spec);

            var items = transactions.Select(t => new WalletTransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type.ToString(),
                ReferenceId = t.ReferenceId,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList();

            return Result.Success(new PaginatedResult<WalletTransactionDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        public async Task<Result<WalletTransactionDto>> AdminAdjustBalanceAsync(int adminId, int userId, decimal amount, string description)
        {
            if (amount == 0)
            {
                return Result<WalletTransactionDto>.Failure("INVALID_AMOUNT", "Amount cannot be zero.");
            }

            var user = await unitOfWork.Repository<User>().GetByIdAsync(userId);
            if (user == null)
            {
                return Result<WalletTransactionDto>.Failure("USER_ERROR", "User not found.");
            }

            var transaction = new WalletTransaction
            {
                UserId = userId,
                Amount = amount,
                Type = TransactionType.ADMIN_ADJUSTMENT,
                ReferenceId = $"ADMIN-{adminId}",
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            unitOfWork.Repository<WalletTransaction>().Add(transaction);
            var result = await unitOfWork.Complete();

            if (result <= 0)
                return Result<WalletTransactionDto>.Failure("DATABASE_ERROR", "Failed to save transaction.");

            return Result.Success(new WalletTransactionDto
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Type = transaction.Type.ToString(),
                ReferenceId = transaction.ReferenceId,
                Description = transaction.Description,
                CreatedAt = transaction.CreatedAt
            });
        }

        public async Task<Result<PaginatedResult<AdminWalletDto>>> AdminGetAllWalletsAsync(int pageNumber, int pageSize)
        {
            var spec = new AllUsersWithTransactionsSpecification(pageNumber, pageSize);
            var users = await unitOfWork.Repository<User>().ListAsync(spec);
            var totalCount = await unitOfWork.Repository<User>().CountAsync(x => true);

            var items = users.Select(u => new AdminWalletDto
            {
                UserId = u.Id,
                UserEmail = u.Email,
                UserFullName = u.FullName,
                Balance = u.WalletTransactions?.Sum(t => t.Amount) ?? 0,
                TransactionCount = u.WalletTransactions?.Count ?? 0,
                LastTransactionDate = u.WalletTransactions?.OrderByDescending(t => t.CreatedAt).FirstOrDefault()?.CreatedAt
            }).ToList();

            return Result.Success(new PaginatedResult<AdminWalletDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        public async Task<Result<int>> DetuctBalanceAync(int userId, decimal amount, string description)
        {
            if (amount <= 0)
            {
                return Result<int>.Failure("DEDUCTION_ERROR", "Deduction amount must be greater than zero.");
            }
            var balanceResult = await GetUserBalanceAsync(userId);
            if (balanceResult.IsFailure)
            {
                return Result<int>.Failure(balanceResult.Error);
            }
            var currentBalance = balanceResult.Value;
            if (currentBalance < amount)
            {
                return Result<int>.Failure("INSUFFICIENT_FUNDS", "User has insufficient funds for this deduction.");
            }
            var transaction = new WalletTransaction
            {
                UserId = userId,
                Amount = -amount,
                Type = TransactionType.DEDUCTION,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };
            unitOfWork.Repository<WalletTransaction>().Add(transaction);
            var result = await unitOfWork.Complete();
            if (result <= 0)
                return Result<int>.Failure("DATABASE_ERROR", "Failed to save deduction transaction to database.");
            return Result.Success(transaction.Id);
        }
    }
}