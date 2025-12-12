using Hangfire.Dashboard;

namespace TorreClou.API.Filters
{
    /// <summary>
    /// Authorization filter for Hangfire Dashboard.
    /// Currently allows all access - modify to add authentication/authorization as needed.
    /// </summary>
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            // TODO: Add authentication/authorization logic here
            // Example: Check if user is authenticated and has admin role
            // var httpContext = context.GetHttpContext();
            // return httpContext.User.Identity?.IsAuthenticated == true 
            //     && httpContext.User.IsInRole("Admin");
            
            // For now, allow all access (consider restricting in production)
            return true;
        }
    }
}

