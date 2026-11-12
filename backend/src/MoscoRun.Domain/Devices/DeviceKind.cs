namespace MoscoRun.Domain.Devices;

/// <summary>Tipo di agente che invia dati al server.</summary>
public enum DeviceKind
{
    /// <summary>Agente Bluetooth collegato a una bilancia.</summary>
    ScaleAgent = 0,

    /// <summary>App Android ponte verso Health Connect.</summary>
    PhoneBridge = 1,
}
