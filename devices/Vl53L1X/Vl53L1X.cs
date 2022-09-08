﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This code has been mostly ported from the Arduino library code
// https://github.com/stm32duino/VL53L1X
// It is based as well on the offical ST Microelectronics API in C
// https://www.st.com/en/embedded-software/stsw-img007.html
using System;
using System.Buffers.Binary;
using System.Device.Gpio;
using System.Device.I2c;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Iot.Device.Vl53L1X
{
    /// <summary>
    ///  Represents Vl53L1X
    /// </summary>
    public class Vl53L1X : IDisposable
    {
        /// <summary>
        ///     The default I2C address
        /// </summary>
        public const ushort DefaultI2cAddress = (ushort)Registers.VL53L1X_DEFAULT_DEVICE_ADDRESS;

        private readonly int _operationTimeoutMilliseconds;
        private readonly int _xShutPin;

        private readonly byte[] _vl53L1XDefaultConfiguration =
        {
            0x00, /* 0x2d : set bit 2 and 5 to 1 for fast plus mode (1MHz I2C), else don't touch */
            0x01, /* 0x2e : bit 0 if I2C pulled up at 1.8V, else set bit 0 to 1 (pull up at AVDD) */
            0x01, /* 0x2f : bit 0 if GPIO pulled up at 1.8V, else set bit 0 to 1 (pull up at AVDD) */
            0x01, /* 0x30 : set bit 4 to 0 for active high interrupt and 1 for active low (bits 3:0 must be 0x1), use SetInterruptPolarity() */
            0x02, /* 0x31 : bit 1 = interrupt depending on the polarity, use CheckForDataReady() */
            0x00, /* 0x32 : not user-modifiable */ 0x02, /* 0x33 : not user-modifiable */
            0x08, /* 0x34 : not user-modifiable */ 0x00, /* 0x35 : not user-modifiable */
            0x08, /* 0x36 : not user-modifiable */ 0x10, /* 0x37 : not user-modifiable */
            0x01, /* 0x38 : not user-modifiable */ 0x01, /* 0x39 : not user-modifiable */
            0x00, /* 0x3a : not user-modifiable */ 0x00, /* 0x3b : not user-modifiable */
            0x00, /* 0x3c : not user-modifiable */ 0x00, /* 0x3d : not user-modifiable */
            0xff, /* 0x3e : not user-modifiable */ 0x00, /* 0x3f : not user-modifiable */
            0x0F, /* 0x40 : not user-modifiable */ 0x00, /* 0x41 : not user-modifiable */
            0x00, /* 0x42 : not user-modifiable */ 0x00, /* 0x43 : not user-modifiable */
            0x00, /* 0x44 : not user-modifiable */ 0x00, /* 0x45 : not user-modifiable */
            0x20, /* 0x46 : interrupt configuration 0->level low detection, 1-> level high, 2-> Out of window, 3->In window, 0x20-> New sample ready , TBC */
            0x0b, /* 0x47 : not user-modifiable */ 0x00, /* 0x48 : not user-modifiable */
            0x00, /* 0x49 : not user-modifiable */ 0x02, /* 0x4a : not user-modifiable */
            0x0a, /* 0x4b : not user-modifiable */ 0x21, /* 0x4c : not user-modifiable */
            0x00, /* 0x4d : not user-modifiable */ 0x00, /* 0x4e : not user-modifiable */
            0x05, /* 0x4f : not user-modifiable */ 0x00, /* 0x50 : not user-modifiable */
            0x00, /* 0x51 : not user-modifiable */ 0x00, /* 0x52 : not user-modifiable */
            0x00, /* 0x53 : not user-modifiable */ 0xc8, /* 0x54 : not user-modifiable */
            0x00, /* 0x55 : not user-modifiable */ 0x00, /* 0x56 : not user-modifiable */
            0x38, /* 0x57 : not user-modifiable */ 0xff, /* 0x58 : not user-modifiable */
            0x01, /* 0x59 : not user-modifiable */ 0x00, /* 0x5a : not user-modifiable */
            0x08, /* 0x5b : not user-modifiable */ 0x00, /* 0x5c : not user-modifiable */
            0x00, /* 0x5d : not user-modifiable */ 0x01, /* 0x5e : not user-modifiable */
            0xcc, /* 0x5f : not user-modifiable */ 0x0f, /* 0x60 : not user-modifiable */
            0x01, /* 0x61 : not user-modifiable */ 0xf1, /* 0x62 : not user-modifiable */
            0x0d, /* 0x63 : not user-modifiable */
            0x01, /* 0x64 : Sigma threshold MSB (mm in 14.2 format for MSB+LSB), use SetSigmaThreshold(), default value 90 mm  */
            0x68, /* 0x65 : Sigma threshold LSB */
            0x00, /* 0x66 : Min count Rate MSB (MCPS in 9.7 format for MSB+LSB), use SetSignalThreshold() */
            0x80, /* 0x67 : Min count Rate LSB */ 0x08, /* 0x68 : not user-modifiable */
            0xb8, /* 0x69 : not user-modifiable */ 0x00, /* 0x6a : not user-modifiable */
            0x00, /* 0x6b : not user-modifiable */
            0x00, /* 0x6c : Intermeasurement period MSB, 32 bits register, use SetIntermeasurementInMs() */
            0x00, /* 0x6d : Intermeasurement period */ 0x0f, /* 0x6e : Intermeasurement period */
            0x89, /* 0x6f : Intermeasurement period LSB */ 0x00, /* 0x70 : not user-modifiable */
            0x00, /* 0x71 : not user-modifiable */
            0x00, /* 0x72 : distance threshold high MSB (in mm, MSB+LSB), use SetD:tanceThreshold() */
            0x00, /* 0x73 : distance threshold high LSB */
            0x00, /* 0x74 : distance threshold low MSB ( in mm, MSB+LSB), use SetD:tanceThreshold() */
            0x00, /* 0x75 : distance threshold low LSB */ 0x00, /* 0x76 : not user-modifiable */
            0x01, /* 0x77 : not user-modifiable */ 0x0f, /* 0x78 : not user-modifiable */
            0x0d, /* 0x79 : not user-modifiable */ 0x0e, /* 0x7a : not user-modifiable */
            0x0e, /* 0x7b : not user-modifiable */ 0x00, /* 0x7c : not user-modifiable */
            0x00, /* 0x7d : not user-modifiable */ 0x02, /* 0x7e : not user-modifiable */
            0xc7, /* 0x7f : ROI center, use SetROI() */ 0xff, /* 0x80 : XY ROI (X=Width, Y=Height), use SetROI() */
            0x9B, /* 0x81 : not user-modifiable */ 0x00, /* 0x82 : not user-modifiable */
            0x00, /* 0x83 : not user-modifiable */ 0x00, /* 0x84 : not user-modifiable */
            0x01, /* 0x85 : not user-modifiable */ 0x00, /* 0x86 : clear interrupt, use ClearInterrupt() */
            0x00 /* 0x87 : start ranging, use StartRanging() or StopRanging(), If you want an automatic start after VL53L1X_init() call, put 0x40 in location 0x87 */
        };

        private I2cDevice _i2CDevice = null!;
        private bool _rangingInitialized;
        private GpioController? _controller;

        /// <summary>
        /// Creates a Vl53L1X sensor class
        /// </summary>
        /// <param name="i2cAddress">The I2C address for the device</param>
        /// <param name="busId">The id of the I2C bus</param>
        /// <param name="xShutPin">
        /// The xShut pin number used to power up the device.
        /// If omitted, The xShutPin must be tied to the power supply value through a pull up resistor
        /// </param>
        /// <param name="operationTimeoutMilliseconds">Timeout for reading data, by default 500 milliseconds</param>
        public Vl53L1X(byte i2cAddress = (byte)Registers.VL53L1X_DEFAULT_DEVICE_ADDRESS, int busId = 1, int xShutPin = -1, int operationTimeoutMilliseconds = 500)
        {
            _xShutPin = xShutPin;
            _operationTimeoutMilliseconds = operationTimeoutMilliseconds;

            PowerOn();
            ChangeI2CAddress(i2cAddress, busId);
            WaitForBooted();
            InitSensor();
        }

        /// <summary>
        ///     Get a distance in millimeters.
        ///     If ranging has not been started yet, the function will automatically start the ranging feature of the device.
        /// </summary>
        public ushort Distance
        {
            get
            {
                if (!_rangingInitialized)
                {
                    StartRanging();
                }

                WaitForDataReady();
                ushort distance = GetDistance();
                return distance;
            }
        }

        private void PowerOn()
        {
            if (_xShutPin < 0)
            {
                return;
            }

            _controller = new GpioController();
            _controller.OpenPin(_xShutPin, PinMode.Output);
            _controller.Write(_xShutPin, PinValue.Low);
            Thread.Sleep(10);
            _controller.Write(_xShutPin, PinValue.High);
            Thread.Sleep(10);
        }

        private void PowerOff()
        {
            if (_xShutPin < 0 || _controller == null)
            {
                return;
            }

            _controller.Write(_xShutPin, PinValue.Low);
            Thread.Sleep(10);
            _controller.ClosePin(_xShutPin);
            _controller.Dispose();
            _controller = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _i2CDevice.Dispose();
            PowerOff();
        }

        private void WaitForBooted()
        {
            BootState bootState = GetBootState();
            Stopwatch watch = Stopwatch.StartNew();

            while (bootState != BootState.Booted && watch.ElapsedMilliseconds < _operationTimeoutMilliseconds)
            {
                Thread.Sleep(10);
                bootState = GetBootState();
            }

            if (bootState != BootState.Booted)
            {
                throw new IOException(
                    $"The device did not boot withing the specified timeout of {_operationTimeoutMilliseconds} ms");

            }
        }

        private void ChangeI2CAddress(byte newAddress, int busId)
        {
            if (newAddress > 0x7F)
            {
                throw new ArgumentException("Value can't exceed 0x7F", nameof(newAddress));
            }

            var defaultAddressDevice = I2cDevice.Create(new I2cConnectionSettings(busId, (byte)Registers.VL53L1X_DEFAULT_DEVICE_ADDRESS));

            if (newAddress == (byte)Registers.VL53L1X_DEFAULT_DEVICE_ADDRESS)
            {
                _i2CDevice = defaultAddressDevice;
                return;
            }

            try
            {
                SpanByte writeArray = new byte[3];
                BinaryPrimitives.WriteUInt16BigEndian(writeArray, (byte)Registers.VL53L1X_I2C_SLAVE__DEVICE_ADDRESS);
                writeArray[2] = newAddress;
                defaultAddressDevice.Write(writeArray);
                Thread.Sleep(10);
            }
            catch (IOException ex)
            {
                throw new IOException($"Can't change I2C Address to {newAddress}", ex);
            }
            finally
            {
                defaultAddressDevice.Dispose();
            }

            _i2CDevice = I2cDevice.Create(new I2cConnectionSettings(busId, newAddress));
        }

        private void InitSensor()
        {
            for (byte register = 0x2D; register <= 0x87; register++)
            {
                WriteRegister(register, _vl53L1XDefaultConfiguration[register - 0x2D]);
            }

            StartRanging();
            WaitForDataReady();

            ClearInterrupt();
            StopRanging();
            WriteRegister((ushort)Registers.VL53L1X_VHV_CONFIG__TIMEOUT_MACROP_LOOP_BOUND, 0x9); /* two bounds VHV */
            WriteRegister(0xB, 0); /* start VHV from the previous temperature */
        }

        /// <summary>
        ///     This function starts the ranging distance operation which is continuous.
        ///     The clear interrupt has to be done after each "get data" to allow the interrupt to be raised when the next data are
        ///     ready.
        ///     1 = active high (default), 0 =active low.
        ///     If required, use <see cref="SetInputPolarity" /> to change the interrupt polarity.
        /// </summary>
        public void StartRanging()
        {
            ClearInterrupt();
            WriteRegister((ushort)Registers.SYSTEM__MODE_START, 0x40);
            _rangingInitialized = true;
        }

        /// <summary>
        ///     This function stops the ranging.
        /// </summary>
        public void StopRanging()
        {
            WriteRegister((ushort)Registers.SYSTEM__MODE_START, 0x00);
            _rangingInitialized = false;
        }

        /// <summary>
        ///     This function returns whether to raning is active or not
        /// </summary>
        /// <returns></returns>
        public bool IsRangingInitialized()
        {
            byte systemMode = ReadByte((ushort)Registers.SYSTEM__MODE_START);
            return systemMode == 0x40;
        }

        /// <summary>
        ///     This function checks if the new ranging data are available by polling the dedicated register.
        /// </summary>
        /// <returns>True if data is ready, otherwise false</returns>
        private bool IsDataReady()
        {
            byte temp = ReadByte((byte)Registers.GPIO__TIO_HV_STATUS);
            return temp != 0; // (temp & 1) == inputPolarity;
        }

        /// <summary>
        ///     This function programs the interrupt polarity
        /// </summary>
        /// <returns>true = active high (default), false = active low</returns>
        public PinValue GetInterruptPolarity()
        {
            byte polarity = ReadByte((ushort)Registers.GPIO_HV_MUX__CTRL);
            polarity = (byte)(polarity & 0x10);
            return polarity >> 4 == 1 ? PinValue.High : PinValue.Low;
        }

        /// <summary>
        ///     This function programs the interrupt polarity.
        /// </summary>
        /// <param name="polarity">The polarity to program. PnValue.High (default), PinValue.Low</param>
        public void SetInputPolarity(PinValue polarity)
        {
            byte temp = ReadByte((ushort)Registers.GPIO_HV_MUX__CTRL);
            temp = (byte)(temp & 0xEF);
            WriteRegister((ushort)Registers.GPIO_HV_MUX__CTRL, (byte)(temp | (byte)(((byte)polarity & 1) << 4)));
        }

        /// <summary>
        ///     This function clears the interrupt to be called after a ranging data reading, to arm the interrupt for the next
        ///     data ready event.
        /// </summary>
        public void ClearInterrupt()
        {
            WriteRegister((ushort)Registers.SYSTEM__INTERRUPT_CLEAR, 0x01);
        }

        /// <summary>
        ///     This function programs the timing budget in ms.
        ///     The predefined values are 15, 33, 20, 50, 100, 200, and 500 <see cref="TimingBudget" />.
        ///     This function must be called after the <see cref="SetDistanceMode" />
        /// </summary>
        /// <param name="budget">The timing budget.</param>
        /// <exception cref="ArgumentException">If timing budget of 15 is set when in distance mode long.</exception>
        public void SetTimingBudgetInMs(TimingBudget budget)
        {
            DistanceMode distanceMode = GetDistanceMode();
            switch (distanceMode)
            {
                case DistanceMode.Short:
                    switch (budget)
                    {
                        case TimingBudget.Budget15:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x01D);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x0027);
                            break;
                        case TimingBudget.Budget20:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x0051);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x006E);
                            break;
                        case TimingBudget.Budget33:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x00D6);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x006E);
                            break;
                        case TimingBudget.Budget50:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x1AE);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x01E8);
                            break;
                        case TimingBudget.Budget100:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x02E1);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x0388);
                            break;
                        case TimingBudget.Budget200:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x03E1);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x0496);
                            break;
                        case TimingBudget.Budget500:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x0591);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x05C1);
                            break;
                    }

                    break;
                case DistanceMode.Long:
                    switch (budget)
                    {
                        case TimingBudget.Budget20:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x001E);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x0022);
                            break;
                        case TimingBudget.Budget33:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x0060);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x006E);
                            break;
                        case TimingBudget.Budget50:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x00AD);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x00C6);
                            break;
                        case TimingBudget.Budget100:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x01CC);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x01EA);
                            break;
                        case TimingBudget.Budget200:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x02D9);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x02F8);
                            break;
                        case TimingBudget.Budget500:
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI, 0x048F);
                            WriteUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_B_HI, 0x04A4);
                            break;
                        default:
                            throw new ArgumentException(
                                $"A timing budget of {TimingBudget.Budget15} can only be set in distance mode {DistanceMode.Short}.",
                                nameof(budget));
                    }

                    break;
            }
        }

        /// <summary>
        ///     This function returns the current <see cref="TimingBudget" /> in ms.
        /// </summary>
        /// <returns>The current <see cref="TimingBudget" /></returns>
        public TimingBudget GetTimingBudgetInMs()
        {
            ushort temp = ReadUInt16((ushort)Registers.RANGE_CONFIG__TIMEOUT_MACROP_A_HI);
            switch (temp)
            {
                case 0x001D:
                    return TimingBudget.Budget15;
                case 0x0051:
                case 0x001E:
                    return TimingBudget.Budget20;
                case 0x00D6:
                case 0x0060:
                    return TimingBudget.Budget33;
                case 0x1AE:
                case 0x00AD:
                    return TimingBudget.Budget50;
                case 0x02E1:
                case 0x01CC:
                    return TimingBudget.Budget100;
                case 0x03E1:
                case 0x02D9:
                    return TimingBudget.Budget200;
                case 0x0591:
                case 0x048F:
                    return TimingBudget.Budget500;
                default:
                    return TimingBudget.BudgetUnknown;
            }
        }

        /// <summary>
        ///     This function programs the distance mode (1 = Short, 2 = Long).
        ///     Short mode maximum distance is limited to 1.3m but results in a better ambient immunity.
        ///     Long mode can range up to 4 m in the dark with a timing budget of200 ms.
        /// </summary>
        /// <param name="mode">The mode to program</param>
        public void SetDistanceMode(DistanceMode mode)
        {
            TimingBudget timingBudget = GetTimingBudgetInMs();

            switch (mode)
            {
                case DistanceMode.Short:
                    WriteRegister((ushort)Registers.PHASECAL_CONFIG__TIMEOUT_MACROP, 0x14);
                    WriteRegister((ushort)Registers.RANGE_CONFIG__VCSEL_PERIOD_A, 0x07);
                    WriteRegister((ushort)Registers.RANGE_CONFIG__VCSEL_PERIOD_B, 0x05);
                    WriteRegister((ushort)Registers.RANGE_CONFIG__VALID_PHASE_HIGH, 0x38);
                    WriteUInt16((ushort)Registers.SD_CONFIG__WOI_SD0, 0x0705);
                    WriteUInt16((ushort)Registers.SD_CONFIG__INITIAL_PHASE_SD0, 0x0606);
                    break;
                case DistanceMode.Long:
                    WriteRegister((ushort)Registers.PHASECAL_CONFIG__TIMEOUT_MACROP, 0x0A);
                    WriteRegister((ushort)Registers.RANGE_CONFIG__VCSEL_PERIOD_A, 0x0F);
                    WriteRegister((ushort)Registers.RANGE_CONFIG__VCSEL_PERIOD_B, 0x0D);
                    WriteRegister((ushort)Registers.RANGE_CONFIG__VALID_PHASE_HIGH, 0xB8);
                    WriteUInt16((ushort)Registers.SD_CONFIG__WOI_SD0, 0x0F0D);
                    WriteUInt16((ushort)Registers.SD_CONFIG__INITIAL_PHASE_SD0, 0x0E0E);
                    break;
            }

            SetTimingBudgetInMs(timingBudget);
        }

        /// <summary>
        ///     This function returns the current distance mode (1 = Short, 2 = Long).
        /// </summary>
        /// <returns>The currently used distance mode.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If reported distance mode is unknown</exception>
        public DistanceMode GetDistanceMode()
        {
            byte temp = ReadByte((ushort)Registers.PHASECAL_CONFIG__TIMEOUT_MACROP);
            if (temp != 0x14 && temp != 0xA)
            {
                return DistanceMode.Unknown;
            }

            return temp == 0x14 ? DistanceMode.Short : DistanceMode.Long;
        }

        /// <summary>
        ///     This function programs the intermeasurement period in ms.
        ///     Intermeasurement period must be >/= timing budget. This condition is not checked by the API,
        ///     the customer has the duty to check the condition. Default = 100 ms.
        /// </summary>
        /// <param name="interMeasMs">The inermeasurement period to set.</param>
        public void SetInterMeasurementInMs(ushort interMeasMs)
        {
            uint clockPll = ReadUInt32((ushort)Registers.VL53L1X_RESULT__OSC_CALIBRATE_VAL);
            clockPll &= 0x3FF;
            WriteUInt32((ushort)Registers.VL53L1X_SYSTEM__INTERMEASUREMENT_PERIOD,
                (uint)(clockPll * interMeasMs * 1.075));
        }

        /// <summary>
        ///     This function returns the Intermeasurement period in ms.
        /// </summary>
        /// <returns>The Intermeasurement period in ms.</returns>
        public ushort GetInterMeasurementInMs()
        {
            uint temp = ReadUInt32((ushort)Registers.VL53L1X_SYSTEM__INTERMEASUREMENT_PERIOD);
            uint clockPll = ReadUInt32((ushort)Registers.VL53L1X_RESULT__OSC_CALIBRATE_VAL);

            clockPll &= 0x3FF;

            return (ushort)(temp / (clockPll * 1.065));
        }

        /// <summary>
        ///     This function returns the <see cref="BootState" /> of the device (1 = booted, 0 = not booted).
        /// </summary>
        /// <returns>The <see cref="BootState" /> of the device.</returns>
        public BootState GetBootState()
        {
            byte bootstateVal = ReadByte((ushort)Registers.VL53L1X_FIRMWARE__SYSTEM_STATUS);
            return
                bootstateVal > 0
                    ? BootState.Booted
                    : BootState.NotBooted; // (BootState)ReadByte((ushort)Registers.VL53L1X_FIRMWARE__SYSTEM_STATUS);
        }

        /// <summary>
        ///     This function returns the sensor ID which must be 0xEEAC.
        /// </summary>
        /// <returns>The sensor id.</returns>
        public ushort GetSensorId()
        {
            return ReadUInt16((ushort)Registers.VL53L1X_IDENTIFICATION__MODEL_ID);
        }

        /// <summary>
        ///     This function returns the distance measured by the sensor in mm.
        /// </summary>
        /// <returns>The distance in mm.</returns>
        public ushort GetDistance()
        {
            ushort distance = ReadUInt16((ushort)Registers.VL53L1X_RESULT__FINAL_CROSSTALK_CORRECTED_RANGE_MM_SD0);
            ClearInterrupt();
            return distance;
        }

        /// <summary>
        ///     This function gives the returned signal per SPAD in kcps/SPAD.
        /// </summary>
        /// <returns>The signal per SPAD in kcps/SPAD</returns>
        public ushort GetSignalPerSpad()
        {
            ushort signal =
                ReadUInt16((ushort)Registers.VL53L1X_RESULT__PEAK_SIGNAL_COUNT_RATE_CROSSTALK_CORRECTED_MCPS_SD0);
            ushort spNb = ReadUInt16((ushort)Registers.VL53L1X_RESULT__DSS_ACTUAL_EFFECTIVE_SPADS_SD0);
            return (ushort)(2000.0 * signal / spNb);
        }

        /// <summary>
        ///     This function returns the ambient per SPAD in kcps/SPAD.
        /// </summary>
        /// <returns>The ambient per SPAD in kcps/SPAD.</returns>
        public ushort GetAmbientPerSpad()
        {
            ushort ambientRate = ReadUInt16((ushort)Registers.RESULT__AMBIENT_COUNT_RATE_MCPS_SD);
            ushort spNb = ReadUInt16((ushort)Registers.VL53L1X_RESULT__DSS_ACTUAL_EFFECTIVE_SPADS_SD0);
            return (ushort)(2000.0 * ambientRate / spNb);
        }

        /// <summary>
        ///     This function returns the signal in kcps.
        /// </summary>
        /// <returns>the signal in kcps.</returns>
        public ushort GetSignalRate()
        {
            return (ushort)(ReadUInt16((ushort)Registers
                .VL53L1X_RESULT__PEAK_SIGNAL_COUNT_RATE_CROSSTALK_CORRECTED_MCPS_SD0) * 8);
        }

        /// <summary>
        ///     This function returns the current number of enabled SPADs.
        /// </summary>
        /// <returns>the current number of enabled SPADs.</returns>
        public ushort GetSpadNb()
        {
            return (ushort)(ReadUInt16((ushort)Registers.VL53L1X_RESULT__DSS_ACTUAL_EFFECTIVE_SPADS_SD0) >> 8);
        }

        /// <summary>
        ///     This function returns the ambient rate in kcps.
        /// </summary>
        /// <returns>The ambient rate in kcps.</returns>
        public ushort GetAmbientRate()
        {
            return (ushort)(ReadUInt16((ushort)Registers.RESULT__AMBIENT_COUNT_RATE_MCPS_SD) * 8);
        }

        /// <summary>
        ///     This function returns the <see cref="RangeStatus" /> of the device.
        /// </summary>
        /// <returns>The <see cref="RangeStatus" /></returns>
        public RangeStatus GetRangeStatus()
        {
            byte rgSt = ReadByte((ushort)Registers.VL53L1X_RESULT__RANGE_STATUS);

            rgSt = (byte)(rgSt & 0x1F);

            switch (rgSt)
            {
                case 9:
                    return RangeStatus.NoError;
                case 6:
                    return RangeStatus.SigmaFailure;
                case 4:
                    return RangeStatus.SignalFailure;
                case 5:
                    return RangeStatus.OutOfBounds;
                case 7:
                    return RangeStatus.WrapAround;
                default:
                    throw new Exception($"The returned range status code {rgSt} of the device is unknown.");
            }
        }

        /// <summary>
        ///     This function programs the offset correction in mm.
        /// </summary>
        /// <param name="offsetValue">The offset correction value to program in mm</param>
        public void SetOffset(short offsetValue)
        {
            WriteInt16((ushort)Registers.ALGO__PART_TO_PART_RANGE_OFFSET_MM, (short)(offsetValue * 4));
            WriteInt16((ushort)Registers.MM_CONFIG__INNER_OFFSET_MM, 0x0);
            WriteInt16((ushort)Registers.MM_CONFIG__OUTER_OFFSET_MM, 0x0);
        }

        /// <summary>
        ///     This function returns the programmed offset correction value in mm.
        /// </summary>
        /// <returns>The offset correction value in mm.</returns>
        public short GetOffset()
        {
            short offset = ReadInt16((ushort)Registers.ALGO__PART_TO_PART_RANGE_OFFSET_MM);
            offset <<= 3;
            offset /= 32;
            return offset;
        }

        /// <summary>
        ///     This function programs the crosstalk correction value in cps.
        ///     This is the number of photons reflected back from the cover glass in cps.
        /// </summary>
        /// <param name="xTalkValue">The crosstalk correction value in cps.</param>
        public void SetXtalk(ushort xTalkValue)
        {
            WriteUInt16((ushort)Registers.ALGO__CROSSTALK_COMPENSATION_X_PLANE_GRADIENT_KCPS, 0x0000);
            WriteUInt16((ushort)Registers.ALGO__CROSSTALK_COMPENSATION_Y_PLANE_GRADIENT_KCPS, 0x0000);
            WriteUInt16((ushort)Registers.ALGO__CROSSTALK_COMPENSATION_PLANE_OFFSET_KCPS,
                (ushort)((xTalkValue << 9) / 1000)); /* * << 9 (7.9 format) and /1000 to convert cps to kpcs */
        }

        /// <summary>
        ///     This function returns the current programmed crosstalk correction value in cps.
        /// </summary>
        /// <returns>The crosstalk correction value in cps.</returns>
        public ushort GetXtalk()
        {
            ushort xTalk = ReadUInt16((ushort)Registers.ALGO__CROSSTALK_COMPENSATION_PLANE_OFFSET_KCPS);

            return (ushort)((xTalk * 1000) >> 9); /* * 1000 to convert kcps to cps and >> 9 (7.9 format) */
        }

        /// <summary>
        ///     This function programs the threshold detection mode.
        ///     For example:
        ///     SetDistanceThreshold(dev,100,300, WindowDetectionMode.Below): below 100
        ///     SetDistanceThreshold(dev,100,300, WindowDetectionMode.Above): above 300
        ///     SetDistanceThreshold(dev,100,300, WindowDetectionMode.Out): out-of-window
        ///     SetDistanceThreshold(dev,100,300, WindowDetectionMode.In): in window
        /// </summary>
        /// <param name="threshLow">
        ///     The threshold under which the device raises an interrupt if detectionMode =
        ///     WindowDetectionMode.Below
        /// </param>
        /// <param name="threshHigh">
        ///     The threshold above which the device raises an interrupt if detectionMode =
        ///     WindowDetectionMode.Above
        /// </param>
        /// <param name="detectionMode">The <see cref="WindowDetectionMode" /> where 0 = below, 1 = above, 2 = out, and 3 = in</param>
        public void SetDistanceThreshold(ushort threshLow, ushort threshHigh, WindowDetectionMode detectionMode)
        {
            byte temp = ReadByte((ushort)Registers.SYSTEM__INTERRUPT_CONFIG_GPIO);
            temp &= 0x47;
            WriteRegister((ushort)Registers.SYSTEM__INTERRUPT_CONFIG_GPIO,
                (byte)(temp | ((byte)detectionMode & 0x07) | 0x40));
            WriteUInt16((ushort)Registers.SYSTEM__THRESH_HIGH, threshHigh);
            WriteUInt16((ushort)Registers.SYSTEM__THRESH_LOW, threshLow);
        }

        /// <summary>
        ///     This function returns the <see cref="WindowDetectionMode" />.
        /// </summary>
        /// <returns>The <see cref="WindowDetectionMode" /></returns>
        public WindowDetectionMode GetDistanceThresholdWindowDetectionMode()
        {
            byte temp = ReadByte((ushort)Registers.SYSTEM__INTERRUPT_CONFIG_GPIO);
            return
                (WindowDetectionMode)(temp &
                                      0x7); // todo: Check if this returns the right value (see set function above which ors and ands slightly different!)
        }

        /// <summary>
        ///     This function returns the low threshold in mm.
        /// </summary>
        /// <returns>The low threshold in mm.</returns>
        public ushort GetDistanceThresholdLow()
        {
            return ReadUInt16((ushort)Registers.SYSTEM__THRESH_LOW);
        }

        /// <summary>
        ///     This function returns the high threshold in mm.
        /// </summary>
        /// <returns>The high threshold in mm.</returns>
        public ushort GetDistanceThresholdHigh()
        {
            return ReadUInt16((ushort)Registers.SYSTEM__THRESH_HIGH);
        }

        /// <summary>
        ///     This function programs the <see cref="Roi" />, the position of which is centered about the optical center. The
        ///     smallest acceptable ROI size is 4.
        /// </summary>
        /// <param name="roi">The <see cref="Roi" /> to set.</param>
        public void SetRoi(Roi roi)
        {
            byte opticalCenter = ReadByte((ushort)Registers.VL53L1X_ROI_CONFIG__MODE_ROI_CENTRE_SPAD);
            ushort x = roi.Width;
            ushort y = roi.Height;

            if (x > 16)
            {
                x = 16;
            }

            if (y > 16)
            {
                y = 16;
            }

            if (x > 10 || y > 10)
            {
                opticalCenter = 199;
            }

            WriteRegister((ushort)Registers.ROI_CONFIG__USER_ROI_CENTRE_SPAD, opticalCenter);
            WriteRegister((ushort)Registers.ROI_CONFIG__USER_ROI_REQUESTED_GLOBAL_XY_SIZE,
                (byte)(((y - 1) << 4) | (x - 1)));
        }

        /// <summary>
        ///     This function returns the <see cref="Roi" /> width and height.
        /// </summary>
        /// <returns>The current <see cref="Roi" /> of the device.</returns>
        public Roi GetRoi()
        {
            byte temp = ReadByte((ushort)Registers.ROI_CONFIG__USER_ROI_REQUESTED_GLOBAL_XY_SIZE);

            return new Roi((ushort)((temp & 0x0F) + 1), (ushort)(((temp & 0xF0) >> 4) + 1));
        }

        /// <summary>
        ///     This function programs the new user ROI center, please to be aware that there is no check in this function
        ///     if the ROI center vs ROI size is out of border.
        /// </summary>
        /// <param name="center">The ROI center</param>
        public void SetRoiCenter(byte center)
        {
            WriteRegister((ushort)Registers.ROI_CONFIG__USER_ROI_CENTRE_SPAD, center);
        }

        /// <summary>
        ///     This function returns the current user ROI center.
        /// </summary>
        /// <returns>The current user ROI center.</returns>
        public byte GetRoiCenter()
        {
            return ReadByte((ushort)Registers.ROI_CONFIG__USER_ROI_CENTRE_SPAD);
        }

        /// <summary>
        ///     This function programs a new signal threshold in kcps where the default is 1024 kcps.
        /// </summary>
        /// <param name="signal">The signal threshold in kcps.</param>
        public void SetSignalThreshold(ushort signal)
        {
            WriteUInt16((ushort)Registers.RANGE_CONFIG__MIN_COUNT_RATE_RTN_LIMIT_MCPS, (ushort)(signal >> 3));
        }

        /// <summary>
        ///     This function returns the current signal threshold in kcps.
        /// </summary>
        /// <returns>The current signal threshold in kcps.</returns>
        public ushort GetSignalThreshold()
        {
            return (ushort)(ReadUInt16((ushort)Registers.RANGE_CONFIG__MIN_COUNT_RATE_RTN_LIMIT_MCPS) << 3);
        }

        /// <summary>
        ///     This function programs a new sigma threshold in mm. The default value is 15 mm.
        /// </summary>
        /// <param name="sigma">The sigma threshold in mm.</param>
        public void SetSigmaThreshold(ushort sigma)
        {
            if (sigma > 0xFFFF >> 2)
            {
                throw new ArgumentOutOfRangeException(nameof(sigma), "The sigma threshold is too high");
            }

            WriteUInt16((ushort)Registers.RANGE_CONFIG__SIGMA_THRESH, (ushort)(sigma << 2));
        }

        /// <summary>
        ///     This function returns the current sigma threshold in mm.
        /// </summary>
        /// <returns>The current sigma threshold in mm.</returns>
        public ushort GetSigmaThreshold()
        {
            return (ushort)(ReadByte((ushort)Registers.RANGE_CONFIG__SIGMA_THRESH) >> 2);
        }

        /// <summary>
        ///     This function performs the temperature calibration.
        ///     If the sensor has been stopped for a long time, it is recommended to perform the temperature update prior to
        ///     restarting the ranging.
        ///     By default, the sensor can adequately handle any temperature change as long as it is running, but if the sensor is
        ///     stopped for an extended period of time,
        ///     a temperature compensation is advised.
        /// </summary>
        public void StartTemperatureUpdate()
        {
            WriteRegister((ushort)Registers.VL53L1X_VHV_CONFIG__TIMEOUT_MACROP_LOOP_BOUND, 0x81); /* full VHV */
            WriteRegister(0x0B, 0x92);
            StartRanging();

            WaitForDataReady();

            ClearInterrupt();
            StopRanging();
            WriteRegister((ushort)Registers.VL53L1X_VHV_CONFIG__TIMEOUT_MACROP_LOOP_BOUND, 0x09); /* two bounds VHV */
            WriteRegister(0x0B, 0); /* start VHV from the previous temperature */
        }

        /// <summary>
        ///     This function performs the offset calibration and programs the offset compensation into the device.
        ///     Target reflectance should be grey17%
        /// </summary>
        /// <param name="targetDistInMm">Target distance in mm, ST recommended 100 mm</param>
        /// <returns>The offset value found</returns>
        public short CalibrateOffset(ushort targetDistInMm)
        {
            WriteUInt16((ushort)Registers.ALGO__PART_TO_PART_RANGE_OFFSET_MM, 0x0);
            WriteUInt16((ushort)Registers.MM_CONFIG__INNER_OFFSET_MM, 0x0);
            WriteUInt16((ushort)Registers.MM_CONFIG__OUTER_OFFSET_MM, 0x0);
            StartRanging(); /* Enable VL53L1X sensor */
            int averageDistance = 0;

            for (int i = 0; i < 50; i++)
            {
                WaitForDataReady();
                ushort distance = GetDistance();
                ClearInterrupt();
                averageDistance += distance;
            }

            StopRanging();
            averageDistance /= 50;

            short offset = (short)(targetDistInMm - averageDistance);

            WriteInt16((ushort)Registers.ALGO__PART_TO_PART_RANGE_OFFSET_MM, (short)(offset * 4));

            return offset;
        }

        /// <summary>
        ///     This function performs the xtalk calibration and programs the xtalk compensation to the device.
        ///     Target reflectance should be grey 17%
        /// </summary>
        /// <param name="targetDistInMm">
        ///     The target distance in mm.
        ///     This is the distance where the sensor starts to "under range"
        ///     due to the influence of the photons reflected back from the cover glass becoming strong.
        ///     It's also called inflection point
        /// </param>
        /// <returns>The xtalk value found in cps (number of photons in count per second).</returns>
        public ushort CalibrateXtalk(ushort targetDistInMm)
        {
            WriteUInt16(0x0016, 0);
            StartRanging();
            int averageSignalRate = 0;
            int averageDistance = 0;
            int averageSpadNb = 0;

            for (int i = 0; i < 50; i++)
            {
                WaitForDataReady();
                ushort sr = GetSignalRate();
                ushort distance = GetDistance();
                ushort spadNum = GetSpadNb();

                ClearInterrupt();
                averageDistance += distance;
                averageSignalRate += sr;
                averageSpadNb += spadNum;
            }

            StopRanging();

            averageDistance /= 50;
            averageSignalRate /= 50;
            averageSpadNb /= 50;

            ushort xTalk = (ushort)(512 * averageSignalRate * (1 - averageDistance / targetDistInMm) / averageSpadNb);
            WriteUInt16(0x0016, xTalk);

            return xTalk;
        }

        private void WaitForDataReady()
        {
            bool dataReady = IsDataReady();
            Stopwatch watch = new Stopwatch();
            watch.Start();

            while (!dataReady && watch.ElapsedMilliseconds > _operationTimeoutMilliseconds)
            {
                Thread.Sleep(50);
                dataReady = IsDataReady();
            }

            if (!dataReady)
            {
                throw new TimeoutException(
                    $"The device did not send any data within the specified timeout of {_operationTimeoutMilliseconds}.");

            }
        }

        private void WriteRegister(ushort reg, byte param)
        {
            SpanByte writeArray = new byte[3];
            BinaryPrimitives.WriteUInt16BigEndian(writeArray, reg);
            writeArray[2] = param;
            _i2CDevice.Write(writeArray);
        }

        private byte ReadByte(ushort reg)
        {
            SpanByte writeBytes = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(writeBytes, reg);
            _i2CDevice.Write(writeBytes);
            return _i2CDevice.ReadByte();
        }

        private short ReadInt16(ushort reg)
        {
            SpanByte outArray = new byte[2];
            SpanByte writeArray = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(writeArray, reg);
            _i2CDevice.Write(writeArray);

            _i2CDevice.Read(outArray);
            return BinaryPrimitives.ReadInt16BigEndian(outArray);
        }

        private ushort ReadUInt16(ushort reg)
        {
            SpanByte outArray = new byte[2];
            SpanByte writeArray = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(writeArray, reg);
            _i2CDevice.Write(writeArray);
            _i2CDevice.Read(outArray);
            return BinaryPrimitives.ReadUInt16BigEndian(outArray);
        }

        private uint ReadUInt32(ushort reg)
        {
            SpanByte outArray = new byte[4];
            SpanByte writeArray = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(writeArray, reg);
            _i2CDevice.Write(writeArray);

            _i2CDevice.Read(outArray);
            return BinaryPrimitives.ReadUInt32BigEndian(outArray);
        }

        private void WriteInt16(ushort reg, short data)
        {
            SpanByte outArray = new byte[4];
            BinaryPrimitives.WriteUInt16BigEndian(outArray, reg);
            BinaryPrimitives.WriteInt16BigEndian(outArray.Slice(2), data);
            _i2CDevice.Write(outArray);
        }

        private void WriteUInt16(ushort reg, ushort data)
        {
            SpanByte outArray = new byte[4];
            BinaryPrimitives.WriteUInt16BigEndian(outArray, reg);
            BinaryPrimitives.WriteUInt16BigEndian(outArray.Slice(2), data);
            _i2CDevice.Write(outArray);
        }

        private void WriteInt32(ushort reg, int data)
        {
            SpanByte outArray = new byte[6];
            BinaryPrimitives.WriteUInt16BigEndian(outArray, reg);
            BinaryPrimitives.WriteInt32BigEndian(outArray.Slice(2), data);
            _i2CDevice.Write(outArray);
        }

        private void WriteUInt32(ushort reg, uint data)
        {
            SpanByte outArray = new byte[6];
            BinaryPrimitives.WriteUInt16BigEndian(outArray, reg);
            BinaryPrimitives.WriteUInt32BigEndian(outArray.Slice(2), data);
            _i2CDevice.Write(outArray);
        }
    }
}
