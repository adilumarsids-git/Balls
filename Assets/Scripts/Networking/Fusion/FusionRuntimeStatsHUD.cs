using System.Reflection;
using System.Text;
using Fusion;
using TMPro;
using UnityEngine;

namespace Project.Networking.Fusion
{
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
            _sb.Append("\nRTT: ").Append(ReadRttMs(runner).ToString("0.0")).Append(" ms");
            _sb.Append("\nResim: ").Append(ReadResimCount(runner));
            _sb.Append("\nPacketLoss: ").Append(ReadOptionalPercent(runner, "PacketLoss")).Append('%');
            _sb.Append("\nJitter: ").Append(ReadOptionalMs(runner, "Jitter")).Append(" ms");

            output.text = _sb.ToString();
        }

        private static int ReadTickRate(NetworkRunner runner)
        {
            return runner.Simulation != null ? runner.Simulation.Config.TickRate : 0;
        }

        private static float ReadRttMs(NetworkRunner runner)
        {
            float rttSeconds = runner.GetPlayerRtt(runner.LocalPlayer);
            return rttSeconds * 1000f;
        }

        private static int ReadResimCount(NetworkRunner runner)
        {
            if (runner.Simulation == null)
                return 0;

            PropertyInfo property = runner.Simulation.GetType().GetProperty("ResimulationCount", BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.PropertyType == typeof(int))
                return (int)property.GetValue(runner.Simulation);

            PropertyInfo runnerProperty = runner.GetType().GetProperty("IsResimulation", BindingFlags.Instance | BindingFlags.Public);
            if (runnerProperty != null && runnerProperty.PropertyType == typeof(bool) && (bool)runnerProperty.GetValue(runner))
                return 1;

            return 0;
        }

        private static string ReadOptionalPercent(NetworkRunner runner, string propertyName)
        {
            float value = ReadFloatProperty(runner, propertyName);
            return value < 0f ? "n/a" : (value * 100f).ToString("0.0");
        }

        private static string ReadOptionalMs(NetworkRunner runner, string propertyName)
        {
            float value = ReadFloatProperty(runner, propertyName);
            return value < 0f ? "n/a" : (value * 1000f).ToString("0.0");
        }

        private static float ReadFloatProperty(NetworkRunner runner, string propertyName)
        {
            PropertyInfo property = runner.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return -1f;

            if (property.PropertyType == typeof(float))
                return (float)property.GetValue(runner);

            return -1f;
        }
    }
}
