public class Location
{
    public string State { get; set; }

    public string City { get; set; }

    public override string ToString() => $"{City}, {State}";
}