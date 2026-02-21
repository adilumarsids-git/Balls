using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Fusion;
using TMPro;
using UnityEngine;

namespace Project.Networking.Fusion
{
    /// Lightweight runtime diagnostics that is resilient across Fusion API variants.
    /// It intentionally uses reflection for optional properties/methods that changed between versions.
    public class FusionRuntimeStatsHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text output;
        [SerializeField] private float refreshInterval = 0.2f;

        private readonly StringBuilder _sb = new StringBuilder(256);
        private float _nextRefreshTime;

        private void Update()
        {
            if (output == null)
                return;

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshInterval;

            NetworkRunner runner = FindObjectOfType<NetworkRunner>();
            if (runner == null || !runner.IsRunning)
            {
                output.text = "Runner: offline";
                return;
            }

            _sb.Clear();
            _sb.Append("Runner: ").Append(runner.Mode);
            _sb.Append("\nTickRate: ").Append(ReadTickRate(runner));
            _sb.Append("\nRTT: ").Append(ReadRttMs(runner)).Append(" ms");
            _sb.Append("\nResim: ").Append(ReadResimCount(runner));
            _sb.Append("\nPacketLoss: ").Append(ReadOptionalPercent(runner, "PacketLoss")).Append('%');
            _sb.Append("\nJitter: ").Append(ReadOptionalMs(runner, "Jitter")).Append(" ms");

            output.text = _sb.ToString();
        }

        private static string ReadTickRate(NetworkRunner runner)
        {
            // Try common direct names first.
            if (TryReadIntProperty(runner, "TickRate", out int directTickRate))
                return directTickRate.ToString(CultureInfo.InvariantCulture);

            // Try nested config path variants with reflection only.
            object simulation = ReadPropertyObject(runner, "Simulation");
            if (simulation != null)
            {
                if (TryReadIntProperty(simulation, "TickRate", out int simTickRate))
                    return simTickRate.ToString(CultureInfo.InvariantCulture);

                object simConfig = ReadPropertyObject(simulation, "Config");
                if (simConfig != null && TryReadIntProperty(simConfig, "TickRate", out int configTickRate))
                    return configTickRate.ToString(CultureInfo.InvariantCulture);
            }

            object runnerConfig = ReadPropertyObject(runner, "Config");
            if (runnerConfig != null && TryReadIntProperty(runnerConfig, "TickRate", out int runnerConfigTickRate))
                return runnerConfigTickRate.ToString(CultureInfo.InvariantCulture);

            return "n/a";
        }

        private static string ReadRttMs(NetworkRunner runner)
        {
            MethodInfo method = runner.GetType().GetMethod("GetPlayerRtt", BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
                return "n/a";

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1)
                return "n/a";

            try
            {
                object value = method.Invoke(runner, new object[] { runner.LocalPlayer });
                if (value == null)
                    return "n/a";

                double seconds = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return (seconds * 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
            }
            catch
            {
                return "n/a";
            }
        }

        private static string ReadResimCount(NetworkRunner runner)
        {
            object simulation = ReadPropertyObject(runner, "Simulation");
            if (simulation != null && TryReadIntProperty(simulation, "ResimulationCount", out int count))
                return count.ToString(CultureInfo.InvariantCulture);

            if (TryReadBoolProperty(runner, "IsResimulation", out bool isResim))
                return isResim ? "1" : "0";

            return "n/a";
        }

        private static string ReadOptionalPercent(NetworkRunner runner, string propertyName)
        {
            if (!TryReadDoubleProperty(runner, propertyName, out double value))
                return "n/a";

            return (value * 100.0).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string ReadOptionalMs(NetworkRunner runner, string propertyName)
        {
            if (!TryReadDoubleProperty(runner, propertyName, out double value))
                return "n/a";

            return (value * 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static object ReadPropertyObject(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property != null ? property.GetValue(instance) : null;
        }

        private static bool TryReadIntProperty(object instance, string propertyName, out int value)
        {
            value = 0;
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return false;

            object raw = property.GetValue(instance);
            if (raw == null)
                return false;

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadBoolProperty(object instance, string propertyName, out bool value)
        {
            value = false;
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(bool))
                return false;

            object raw = property.GetValue(instance);
            if (raw == null)
                return false;

            value = (bool)raw;
            return true;
        }

        private static bool TryReadDoubleProperty(object instance, string propertyName, out double value)
        {
            value = 0.0;
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return false;

            object raw = property.GetValue(instance);
            if (raw == null)
                return false;

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
