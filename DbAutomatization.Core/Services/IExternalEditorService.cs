namespace DbAutomatization.Core.Services;

public interface IExternalEditorService
{
    Task<bool> EditConfigInExternalEditorAsync(CancellationToken cancellationToken = default);
}
