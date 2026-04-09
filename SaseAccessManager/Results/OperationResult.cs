namespace SaseAccessManager.Results
{
    public class OperationResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }

        public static OperationResult Ok()
            => new() { Success = true };

        public static OperationResult Fail(string error)
            => new() { Success = false, Error = error };
    }

    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; init; }
        public bool UserAlreadyExistsInSase { get; init; }
        public string? ExistingSaseUserId { get; init; }
        public List<string> ExistingSaseGroupIds { get; init; } = [];

        public static OperationResult<T> Ok(T data)
            => new() { Success = true, Data = data };

        public new static OperationResult<T> Fail(string error)
            => new() { Success = false, Error = error };

        public static OperationResult<T> ExistsInSase(string saseUserId, List<string> groupIds)
            => new()
            {
                Success = false,
                UserAlreadyExistsInSase = true,
                ExistingSaseUserId = saseUserId,
                ExistingSaseGroupIds = groupIds
            };
    }

    public class BatchUserResult
    {
        public string Email { get; init; } = "";
        public bool Success { get; init; }
        public string? Error { get; init; }
    }

    public class BatchOperationResult
    {
        public List<BatchUserResult> Results { get; init; } = [];
        public int SuccessCount => Results.Count(r => r.Success);
        public int FailCount => Results.Count(r => !r.Success);
    }
}
