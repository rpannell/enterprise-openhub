namespace OpenHub.CRM.Api.Managers.Engines.Setup;

public static class EngineServiceExtensions
{
    // No Engines currently — CRM's only Engine (the sales rep hierarchy engine) moved to the
    // SalesOrg module. Kept as a no-op extension method so Program.cs's AddEngines() call and
    // this per-layer DI registration pattern don't need to change if CRM grows a pure-logic
    // Engine of its own later.
    public static IServiceCollection AddEngines(this IServiceCollection services)
        => services;
}
