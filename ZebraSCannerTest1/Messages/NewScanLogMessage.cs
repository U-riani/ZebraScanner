﻿using CommunityToolkit.Mvvm.Messaging.Messages;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Messages;

public class NewScanLogMessage : ValueChangedMessage<ScanLog>
{
    public NewScanLogMessage(ScanLog log) : base(log) { }
}
