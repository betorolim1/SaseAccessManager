namespace SaseAccessManager.DTOs
{
    public class SaseUserDto
    {
        public string Id { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public bool Terminated { get; set; }
    }

    public class SaseUserSearchResponse
    {
        public List<SaseUserDto> Data { get; set; } = [];
    }
}
