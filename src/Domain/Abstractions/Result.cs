namespace Domain.Abstractions;

public class Result
{
    protected Result(bool isSuccess, string error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool   IsSuccess  { get; }
    public string Error      { get; }
    public bool   IsNotFound => !IsSuccess && Error == "Not Found";

    public static Result Success()             => new(true,  string.Empty);
    public static Result Failure(string error) => new(false, error);
    public static Result NotFound()            => new(false, "Not Found");
    public static Result Forbidden()           => new(false, "Forbidden");
}

public class Result<T> : Result
{
    private Result(T value, bool isSuccess, string error) : base(isSuccess, error)
    {
        Value = value;
    }

    public T Value { get; }

    public static Result<T> Success(T value)       => new(value,    true,  string.Empty);
    public new static Result<T> Failure(string e)  => new(default!, false, e);
    public new static Result<T> NotFound()         => new(default!, false, "Not Found");
    public new static Result<T> Forbidden()        => new(default!, false, "Forbidden");
}
