// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Device.Gpio;
using System.Device.I2c;
using System.Diagnostics;
using System.Threading;
using Iot.Device.Vl53L1X;
using nanoFramework.Hardware.Esp32;

const int leftXShutPinNo = 13;
const int leftNewI2CAddress = 0x31;
const int rightXShutPinNo = 2;
const int rightNewI2CAddress = 0x30;

Debug.WriteLine("Hello VL53L1X!");

//////////////////////////////////////////////////////////////////////
// when connecting to an ESP32 device, need to configure the I2C GPIOs
// used for the bus
//Configuration.SetPinFunction(21, DeviceFunction.I2C1_DATA);
//Configuration.SetPinFunction(22, DeviceFunction.I2C1_CLOCK);

Configuration.SetPinFunction(14, DeviceFunction.I2C1_DATA);
Configuration.SetPinFunction(15, DeviceFunction.I2C1_CLOCK);

Configuration.SetPinFunction(18, DeviceFunction.SPI1_CLOCK);
Configuration.SetPinFunction(23, DeviceFunction.SPI1_MOSI);

var gpioController = new GpioController();
gpioController.OpenPin(leftXShutPinNo, PinMode.Output);
gpioController.OpenPin(rightXShutPinNo, PinMode.Output);

gpioController.Write(leftXShutPinNo, PinValue.Low);
gpioController.Write(rightXShutPinNo, PinValue.Low);

Thread.Sleep(10);

gpioController.Write(leftXShutPinNo, PinValue.High);
Thread.Sleep(10);
using var defaultI2CDevice = I2cDevice.Create(new I2cConnectionSettings(1, Vl53L1X.DefaultI2cAddress));
Vl53L1X.ChangeI2CAddress(defaultI2CDevice, leftNewI2CAddress);
Thread.Sleep(10);
using Vl53L1X leftVl53L1X = new(I2cDevice.Create(new I2cConnectionSettings(1, leftNewI2CAddress)));
gpioController.Write(rightXShutPinNo, PinValue.High);
Thread.Sleep(10);
Vl53L1X.ChangeI2CAddress(defaultI2CDevice, rightNewI2CAddress);
Thread.Sleep(10);
using Vl53L1X rightVl53L1X = new(I2cDevice.Create(new I2cConnectionSettings(1, rightNewI2CAddress)));

/*Debug.WriteLine($"Left SensorID: {leftVl53L1X.SensorId:X}");
Debug.WriteLine($"Left Precision: {leftVl53L1X.Precision}");
Debug.WriteLine($"Left TimingBudget: {leftVl53L1X.TimingBudgetInMs}");
leftVl53L1X.Precision = Precision.Short;
Debug.WriteLine($"Left Distance Mode: {leftVl53L1X.Precision}");
Debug.WriteLine($"Left TimingBudget: {leftVl53L1X.TimingBudgetInMs}");
Debug.WriteLine($"Left SpadNb: {leftVl53L1X.SpadNb}");
Debug.WriteLine($"Left InterMeasurementInMs: {leftVl53L1X.InterMeasurementInMs}");*/


/*Debug.WriteLine($"right SensorID: {rightVl53L1X.SensorId:X}");
Debug.WriteLine($"right Precision: {rightVl53L1X.Precision}");
Debug.WriteLine($"right TimingBudget: {rightVl53L1X.TimingBudgetInMs}");
rightVl53L1X.Precision = Precision.Short;
Debug.WriteLine($"right Distance Mode: {rightVl53L1X.Precision}");
Debug.WriteLine($"right TimingBudget: {rightVl53L1X.TimingBudgetInMs}");
Debug.WriteLine($"right SpadNb: {rightVl53L1X.SpadNb}");
Debug.WriteLine($"right InterMeasurementInMs: {rightVl53L1X.InterMeasurementInMs}");*/

while (true)
{
   try
   {
      /*Debug.WriteLine($"Left Distance: {leftVl53L1X.Distance}");
      Debug.WriteLine($"Left RangeStatus {leftVl53L1X.RangeStatus}");*/

      Debug.WriteLine($"right Distance: {rightVl53L1X.Distance}");
      Debug.WriteLine($"right RangeStatus {rightVl53L1X.RangeStatus}");
      
   }
   catch (Exception ex)
   {
      Debug.WriteLine($"Exception: {ex.Message}");
   }

   Thread.Sleep(500);
}
