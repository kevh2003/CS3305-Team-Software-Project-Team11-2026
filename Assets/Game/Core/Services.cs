/*
 * Services
 * 
 * Service locator used to decouple UI/gameplay from concrete implementations
 * UI/gameplay should talk to Services.NetSession (INetSession) rather than NGO APIs
 */

public static class Services
{
    public static INetSession NetSession { get; internal set; }
}