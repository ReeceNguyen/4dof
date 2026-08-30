using CommunityToolkit.Mvvm.ComponentModel;

namespace MobusTCP.Models;

public enum ModbusDataType
{
    Int16,
    UInt16,
    Float32,
    BoolCoil,
    Bitfield16
}

public enum ModbusAccessType
{
    ReadWrite,
    ReadOnly
}

public partial class ModbusRegisterItem : ObservableObject
{
    [ObservableProperty] private int _address;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private ModbusDataType _dataType;
    [ObservableProperty] private ModbusAccessType _accessType;
    [ObservableProperty] private object _value = 0;
    [ObservableProperty] private string _formattedValue = "0";
    [ObservableProperty] private string _unit = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isWritePending;
}
