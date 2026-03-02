namespace SaseAccessManager.DTOs
{
    public class GroupResponse
    {
        public List<GroupItem> Data { get; set; } = [];
    }

    public class GroupItem
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }
}
