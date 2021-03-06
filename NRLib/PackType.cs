namespace NRLib
{
    public enum PackType
    {
        UDP_C_DISCOVER = 1,
        
        UDP_S_DISCOVER_REPLY = 2,
        
        TCP_C_PUBLISH = 4,
        TCP_C_DISCOVER_APP_INSTANCES = 6,
        TCP_C_OPEN_SOCKET = 8,
        
        TCP_S_HELLO = 3,
        TCP_S_PUBLISH_REPLY = 5,
        TCP_S_APP_INSTANCE_REPLY = 7,
        
        TCP_CS_SOCKET_CONTROL = 9,
        TCP_CS_SOCKET_DATA = 10
    }
}