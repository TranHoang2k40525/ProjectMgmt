namespace Planning.Domain.Entities;

public class WorkflowTransition
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FromStatusId { get; set; }
    public Guid ToStatusId { get; set; }
    public string? Name { get; set; }
    public string? RequiredPermissionCode { get; set; }
}
