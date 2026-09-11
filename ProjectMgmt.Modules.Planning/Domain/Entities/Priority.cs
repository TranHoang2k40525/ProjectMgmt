namespace Planning.Domain.Entities;

public class Priority
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string ColorHex { get; set; } = string.Empty;
    public string? IconKey { get; set; }
}
