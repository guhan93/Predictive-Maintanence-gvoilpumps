using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;

namespace Fabrikam.FieldDevice.Generator
{
    public class PumpTelemetry
    {
        public IEnumerable<double> MotorPowerKw { get; set; }
        public IEnumerable<double> MotorSpeed { get; set; }
        public IEnumerable<double> PumpRate { get; set; }
        public IEnumerable<double> TimePumpOn { get; set; }
        public IEnumerable<double> CasingFriction { get; set; }

        public PumpTelemetry() { }

        public PumpTelemetry(IEnumerable<double> motorPowerkW,
            IEnumerable<double> motorSpeed,
            IEnumerable<double> pumpRate,
            IEnumerable<double> timePumpOn,
            IEnumerable<double> casingFriction)
        {
            var transformed = TransformValues(motorPowerkW, motorSpeed, pumpRate, timePumpOn, casingFriction);
            MotorPowerKw = transformed.MotorPowerkW;
            MotorSpeed = transformed.MotorSpeed;
            PumpRate = transformed.PumpRate;
            TimePumpOn = transformed.TimePumpOn;
            CasingFriction = transformed.CasingFriction;
        }

        
        public IEnumerable<PumpTelemetryItem> ToPumpTelemetryItems()
        {
            var numItems = MotorPowerKw.Count();
            var telemetryItems = new List<PumpTelemetryItem>(numItems);
            for (var i = 0; i < numItems; i++)
            {
                telemetryItems.Add(new PumpTelemetryItem(MotorPowerKw.ElementAt(i), MotorSpeed.ElementAt(i),
                    PumpRate.ElementAt(i), TimePumpOn.ElementAt(i), CasingFriction.ElementAt(i)));
            }

            return telemetryItems;
        }

        public void GraduallyDeteriorateNormalToFailed(IEnumerable<double> motorPowerkWNormal,
            IEnumerable<double> motorSpeedNormal,
            IEnumerable<double> pumpRateNormal,
            IEnumerable<double> timePumpOnNormal,
            IEnumerable<double> casingFrictionNormal,
            IEnumerable<double> motorPowerkWFailed,
            IEnumerable<double> motorSpeedFailed,
            IEnumerable<double> pumpRateFailed,
            IEnumerable<double> timePumpOnFailed,
            IEnumerable<double> casingFrictionFailed,
            int failOverXIterations = 250)
        {
            var normal = TransformValues(motorPowerkWNormal, motorSpeedNormal, pumpRateNormal, timePumpOnNormal,
                casingFrictionNormal);
            var failed = TransformValues(motorPowerkWFailed, motorSpeedFailed, pumpRateFailed, timePumpOnFailed,
                casingFrictionFailed);

            if (failOverXIterations > 0)
            {
                var rnd = new Random();
                
                const double motorPowerWobblePercentage = 0.02;
                const double pumpRateWobblePercentage = 0.02;
                const double motorSpeedWobblePercentage = 0.007;
                const double casingFrictionWobblePercentage = 0.002;

                
                var motorPowerkWLastNormal = normal.MotorPowerkW.Last();
                var motorSpeedLastNormal = normal.MotorSpeed.Last();
                var pumpRateLastNormal = normal.PumpRate.Last();
                var timePumpOnLastNormal = normal.TimePumpOn.Last();
                var casingFrictionLastNormal = normal.CasingFriction.Last();

               
                var motorPowerkWFirstFailed = failed.MotorPowerkW.First();
                var motorSpeedFirstFailed = failed.MotorSpeed.First();
                var pumpRateFirstFailed = failed.PumpRate.First();
                var timePumpOnFirstFailed = failed.TimePumpOn.First();
                var casingFrictionFirstFailed = failed.CasingFriction.First();

                
                var motorPowerkWSubtractBy = (motorPowerkWLastNormal - motorPowerkWFirstFailed) / failOverXIterations;
                var motorSpeedSubtractBy = (motorSpeedLastNormal - motorSpeedFirstFailed) / failOverXIterations;
                var pumpRateSubtractBy = (pumpRateLastNormal - pumpRateFirstFailed) / failOverXIterations;
                var casingFrictionSubtractBy =
                    (casingFrictionLastNormal - casingFrictionFirstFailed) / failOverXIterations;

                
                var lastMotorPowerkWValue = motorPowerkWLastNormal;
                var lastMotorSpeedValue = motorSpeedLastNormal;
                var lastPumpRateValue = pumpRateLastNormal;
                var lastTimePumpOnValue = timePumpOnLastNormal;
                var lastCasingFrictionValue = casingFrictionLastNormal;

                
                var motorPowerkWGradualFailure = new List<double>();
                var motorSpeedGradualFailure = new List<double>();
                var pumpRateGradualFailure = new List<double>();
                var timePumpOnGradualFailure = new List<double>();
                var casingFrictionGradualFailure = new List<double>();

                
                var sampleSize = (failOverXIterations / 2) + 1; // Add one for rounding errors.
                var normalToFailureFrequencyDelta =
                    PumpFailedState.TimePumpOn.Frequency - PumpNormalState.TimePumpOn.Frequency;
                var normalToFailureAmplitudeDelta =
                    PumpNormalState.TimePumpOn.Amplitude - PumpFailedState.TimePumpOn.Amplitude;
                
                timePumpOnGradualFailure.AddRange(Generate.Periodic(sampleSize, 10000,
                    normalToFailureFrequencyDelta * 1.5,
                    normalToFailureAmplitudeDelta * 1.5));
                // Create sawtooth delta of normal frequency and amplitude and half fail-over iteration value for the sample size.
                timePumpOnGradualFailure.AddRange(Generate.Periodic(sampleSize, 10000, normalToFailureFrequencyDelta,
                    normalToFailureAmplitudeDelta));

                for (var i = 0; i < failOverXIterations; i++)
                {
                    // Subtract values for this step:
                    lastMotorPowerkWValue -= motorPowerkWSubtractBy;
                    lastMotorSpeedValue -= motorSpeedSubtractBy;
                    lastPumpRateValue -= pumpRateSubtractBy;
                    lastCasingFrictionValue -= casingFrictionSubtractBy;

                    // Add subtracted value to gradual failure collections, or the first failed value, whichever is greater:
                    motorPowerkWGradualFailure.Add(rnd.Next(
                        Convert.ToInt32(lastMotorPowerkWValue) -
                        Convert.ToInt32(lastMotorPowerkWValue * motorPowerWobblePercentage),
                        Convert.ToInt32(lastMotorPowerkWValue) +
                        Convert.ToInt32(lastMotorPowerkWValue * motorPowerWobblePercentage))
                    );
                    motorSpeedGradualFailure.Add(rnd.Next(
                        Convert.ToInt32(lastMotorSpeedValue) -
                        Convert.ToInt32(lastMotorSpeedValue * motorSpeedWobblePercentage),
                        Convert.ToInt32(lastMotorSpeedValue) +
                        Convert.ToInt32(lastMotorSpeedValue * motorSpeedWobblePercentage))
                    );
                    pumpRateGradualFailure.Add(rnd.Next(
                        Convert.ToInt32(lastPumpRateValue) -
                        Convert.ToInt32(lastPumpRateValue * pumpRateWobblePercentage),
                        Convert.ToInt32(lastPumpRateValue) +
                        Convert.ToInt32(lastPumpRateValue * pumpRateWobblePercentage))
                    );
                    casingFrictionGradualFailure.Add(rnd.Next(
                        Convert.ToInt32(lastCasingFrictionValue) -
                        Convert.ToInt32(lastCasingFrictionValue * casingFrictionWobblePercentage),
                        Convert.ToInt32(lastCasingFrictionValue) +
                        Convert.ToInt32(lastCasingFrictionValue * casingFrictionWobblePercentage))
                    );
                }

                // Concatenate the normal, gradual failure, and failure items and save to the class properties.
                MotorPowerKw = normal.MotorPowerkW.Concat(motorPowerkWGradualFailure).Concat(failed.MotorPowerkW);
                MotorSpeed = normal.MotorSpeed.Concat(motorSpeedGradualFailure).Concat(failed.MotorSpeed);
                PumpRate = normal.PumpRate.Concat(pumpRateGradualFailure).Concat(failed.PumpRate);
                TimePumpOn = normal.TimePumpOn.Concat(timePumpOnGradualFailure).Concat(failed.TimePumpOn);
                CasingFriction = normal.CasingFriction.Concat(casingFrictionGradualFailure)
                    .Concat(failed.CasingFriction);
            }
            else
            {
                // Concatenate the normal and failure items for immediate failure, and save to the class properties.
                MotorPowerKw = normal.MotorPowerkW.Concat(failed.MotorPowerkW);
                MotorSpeed = normal.MotorSpeed.Concat(failed.MotorSpeed);
                PumpRate = normal.PumpRate.Concat(failed.PumpRate);
                TimePumpOn = normal.TimePumpOn.Concat(failed.TimePumpOn);
                CasingFriction = normal.CasingFriction.Concat(failed.CasingFriction);
            }
        }

        
        protected (IEnumerable<double> MotorPowerkW,
            IEnumerable<double> MotorSpeed,
            IEnumerable<double> PumpRate,
            IEnumerable<double> TimePumpOn,
            IEnumerable<double> CasingFriction) TransformValues(IEnumerable<double> motorPowerkW,
                IEnumerable<double> motorSpeed,
                IEnumerable<double> pumpRate,
                IEnumerable<double> timePumpOn,
                IEnumerable<double> casingFriction)
        {
            var motorPowerkWTransformed = motorPowerkW.Select(x => Math.Round(x, 2));
            var motorSpeedTransformed = motorSpeed.Select(x => Math.Round(x, 0));
            var pumpRateTransformed = pumpRate.Select(x => Math.Round(x, 1));
            var timePumpOnTransformed = timePumpOn.Select(x => Math.Round(x, 2));
            var casingFrictionTransformed = casingFriction.Select(x => Math.Round(x, 2));

            return (motorPowerkWTransformed, motorSpeedTransformed, pumpRateTransformed, timePumpOnTransformed,
                casingFrictionTransformed);
        }
    }
}