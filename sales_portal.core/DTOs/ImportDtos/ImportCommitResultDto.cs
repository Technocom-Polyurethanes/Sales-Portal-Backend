namespace sales_portal.core.DTOs;

public class ImportCommitResultDto
{
    public Guid ImportId { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}
