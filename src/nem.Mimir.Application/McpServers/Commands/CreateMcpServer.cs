using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Commands;

/// <summary>
/// Command to create a new MCP server configuration.
/// </summary>
public sealed record CreateMcpServerCommand(
    string Name,
    McpTransportType TransportType,
    string? Description,
    string? Command,
    string? Arguments,
    string? Url,
    string? EnvironmentVariablesJson,
    bool IsEnabled = false) : ICommand<Guid>;

internal sealed class CreateMcpServerCommandHandler(
    IMcpServerConfigRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateMcpServerCommand, Guid>
{
    public async Task<Guid> Handle(CreateMcpServerCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(McpTransportType), request.TransportType))
        {
            throw new ValidationException(
                $"Invalid transport type: {(int)request.TransportType}. " +
                $"Valid values are: {string.Join(", ", Enum.GetNames(typeof(McpTransportType)))}.");
        }

        var config = new McpServerConfig
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            TransportType = request.TransportType,
            Description = request.Description,
            Command = request.Command,
            Arguments = request.Arguments,
            Url = request.Url,
            EnvironmentVariablesJson = request.EnvironmentVariablesJson,
            IsEnabled = request.IsEnabled,
            IsBundled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await repository.AddAsync(config, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return config.Id;
    }
}
