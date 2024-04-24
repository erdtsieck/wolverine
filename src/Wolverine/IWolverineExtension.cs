namespace Wolverine;

#region sample_IWolverineExtension

/// <summary>
///     Use to create loadable extensions to Wolverine applications
/// </summary>
public interface IWolverineExtension
{
    /// <summary>
    ///     Make any alterations to the WolverineOptions for the application
    /// </summary>
    /// <param name="options"></param>
    void Configure(WolverineOptions options);
}

#endregion

/// <summary>
/// Loadable extensions to Wolverine applications that may require
/// IoC services or asynchronous operations
/// </summary>
public interface IAsyncWolverineExtension
{
    ValueTask Configure(WolverineOptions options);
}
