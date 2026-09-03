namespace TorrenClou.Core.Exceptions;

public class NotFoundException(string code, string message) : DomainException(code, message);
