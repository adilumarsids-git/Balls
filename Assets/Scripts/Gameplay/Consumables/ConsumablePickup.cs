using UnityEngine;
using Project.Data;
using Project.Gameplay.Player.Stats;

namespace Project.Gameplay.Consumables
{
    public class ConsumablePickup : MonoBehaviour
    {
        [SerializeField] private ConsumableConfigSO config;

        public System.Action<ConsumablePickup> OnConsumed;

        private bool consumed;

        private void OnEnable() => consumed = false;

        private void OnTriggerEnter(Collider other)
        {
            if (consumed) return;

            var buff = other.GetComponentInParent<PlayerBuffApplier>();
            if (buff == null) return;

            consumed = true;

            buff.ApplyBuff(
                config.speedAdd,
                config.massAdd,
                config.sizeAdd,
                config.sizeScaleFactor
            );

            OnConsumed?.Invoke(this);
            Project.Core.Events.GameEvents.RaiseConsumablePicked(buff.gameObject);
            gameObject.SetActive(false);
        }
    }
}
