namespace sales_portal.core.DTOs;

public class SettingsDto
{
    public CompanySettingsDto Company { get; set; } = new();
    public WorkspaceSettingsDto Workspace { get; set; } = new();
}

public class UpdateSettingsDto
{
    public CompanySettingsDto? Company { get; set; }
    public WorkspaceSettingsDto? Workspace { get; set; }
}
