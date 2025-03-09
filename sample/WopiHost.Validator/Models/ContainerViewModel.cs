namespace WopiHost.Validator.Models;

public class ContainerViewModel
{
    public required string ContainerId { get; set; }
    public required string Name { get; set; }
    public DateTime LastModified { get; set; }
}
