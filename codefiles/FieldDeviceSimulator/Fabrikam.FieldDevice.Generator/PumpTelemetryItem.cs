﻿namespace Fabrikam.FieldDevice.Generator
{
    public class PumpTelemetryItem
    {
        public double MotorPowerKw { get; set; }
        public double MotorSpeed { get; set; }
        public double PumpRate { get; set; }
        public double TimePumpOn { get; set; }
        public double CasingFriction { get; set; }
       
        public PumpTelemetryItem(double motorPowerkW, double motorSpeed,
            double pumpRate, double timePumpOn, double casingFriction)
        {
            MotorPowerKw = motorPowerkW;
            MotorSpeed = motorSpeed;
            PumpRate = pumpRate;
            TimePumpOn = timePumpOn;
            CasingFriction = casingFriction;
        }
    }
}