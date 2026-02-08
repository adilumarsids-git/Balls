using UnityEngine;

namespace Project.Gameplay.Player.Movement
{
    /// <summary>Local input provider. Later Fusion will replace/pipe this data.</summary>
    public class PlayerInputTopDown : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public bool BoostPressed { get; private set; }

        private void Update()
        {
            Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (Move.sqrMagnitude > 1f) Move.Normalize();

            BoostPressed = Input.GetKeyDown(KeyCode.Space);
        }

        public void ConsumeBoostPressed() => BoostPressed = false;
    }
}
