namespace Planning.Domain.Entities;

public class BoardColumn
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public Guid StatusId { get; set; }
    public string? Name { get; set; }
    public int OrderIndex { get; set; }
    public int? WipLimit { get; set; }
}
