// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Diagnostics;
using System.Threading;
using Iot.Device.Vl53L1X;

const int xShutPinNo = 23;
const int newI2CAddress = 0x30;

Debug.WriteLine("Hello VL53L1X!");

//////////////////////////////////////////////////////////////////////
// when connecting to an ESP32 device, need to configure the I2C GPIOs
// used for the bus
//Configuration.SetPinFunction(21, DeviceFunction.I2C1_DATA);
//Configuration.SetPinFunction(22, DeviceFunction.I2C1_CLOCK);

using Vl53L1X vl53L1X = new(i2cAddress: newI2CAddress, xShutPin: xShutPinNo);

Debug.WriteLine($"SensorID: {vl53L1X.GetSensorId():X}");
Debug.WriteLine($"Offset in µm: {vl53L1X.GetOffset()}, Signal rate: {vl53L1X.GetSignalRate()}");
Debug.WriteLine($"Distance Mode: {vl53L1X.GetDistanceMode()}");
Debug.WriteLine($"TimingBudget: {vl53L1X.GetTimingBudgetInMs()}");
vl53L1X.SetDistanceMode(DistanceMode.Short);
Debug.WriteLine($"Distance Mode: {vl53L1X.GetDistanceMode()}");
Debug.WriteLine($"TimingBudget: {vl53L1X.GetTimingBudgetInMs()}");
Debug.WriteLine($"SpadNb: {vl53L1X.GetSpadNb()}");
Debug.WriteLine($"InterMeasurementInMs: {vl53L1X.GetInterMeasurementInMs()}");

while (true)
{
   try
   {
      var dist = vl53L1X.Distance;
      var rangeStatus = vl53L1X.GetRangeStatus();
      Debug.WriteLine($"RangeStatus {rangeStatus}");
      Debug.WriteLine($"Distance: {dist}");
   }
   catch (Exception ex)
   {
      Debug.WriteLine($"Exception: {ex.Message}");
   }

   Thread.Sleep(500);
}
