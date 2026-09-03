namespace TorrenClou.Core.Exceptions;

public class UnauthorizedException(string code, string message) : DomainException(code, message);
