namespace EFCore.BulkOperations.Tests.IncludeGraph.Model;

public class Spare
{
    public int Id { get; set; }

    public string PartNumber { get; set; } = null!;
    public string Barcode { get; set; } = null!;
}