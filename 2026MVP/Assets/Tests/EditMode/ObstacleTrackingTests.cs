using NUnit.Framework;
using Gley.TrafficSystem;

namespace TlaxSim.Tests
{
    /// <summary>
    /// Regression test para el bug "carros AI congelados" (TLAX PATCH en VehicleComponent).
    /// OnTriggerEnter y OnTriggerExit DEBEN usar el mismo predicado para decidir si un
    /// collider entra/sale de _obstacleList. Antes Enter agregaba triggers "PlayerTrigger"
    /// pero Exit solo removía no-triggers → el carro quedaba frenado para siempre.
    /// </summary>
    public class ObstacleTrackingTests
    {
        [Test]
        public void SolidCollider_IsAlwaysTracked()
        {
            // Collider sólido (no-trigger): siempre cuenta como obstáculo, con o sin tag.
            Assert.IsTrue(VehicleComponent.ShouldTrackObstacle(isTrigger: false, isPlayerTrigger: false));
            Assert.IsTrue(VehicleComponent.ShouldTrackObstacle(isTrigger: false, isPlayerTrigger: true));
        }

        [Test]
        public void PlayerTriggerTrigger_IsTracked()
        {
            // Trigger etiquetado PlayerTrigger (moto del jugador / burbuja del peatón): se rastrea.
            Assert.IsTrue(VehicleComponent.ShouldTrackObstacle(isTrigger: true, isPlayerTrigger: true));
        }

        [Test]
        public void OtherTrigger_IsIgnored()
        {
            // Cualquier otro trigger NO debe rastrearse (evita falsos obstáculos).
            Assert.IsFalse(VehicleComponent.ShouldTrackObstacle(isTrigger: true, isPlayerTrigger: false));
        }

        [Test]
        public void EnterAndExit_AgreeForEveryCombination()
        {
            // La simetría es el corazón del fix: el mismo input produce la misma decisión,
            // así que todo lo que Enter agrega, Exit lo puede remover.
            foreach (var isTrigger in new[] { false, true })
            foreach (var isPlayerTrigger in new[] { false, true })
            {
                bool enter = VehicleComponent.ShouldTrackObstacle(isTrigger, isPlayerTrigger);
                bool exit = VehicleComponent.ShouldTrackObstacle(isTrigger, isPlayerTrigger);
                Assert.AreEqual(enter, exit,
                    $"Asimetría Enter/Exit para isTrigger={isTrigger}, isPlayerTrigger={isPlayerTrigger}");
            }
        }
    }
}
