using UnityEngine;

namespace Hollowfen.Weather
{
    /// <summary>
    /// Marks an authored trigger as covered space when decorative architecture has an open or
    /// non-colliding roof. WeatherPresentation discovers it without coupling to a location type.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WeatherShelterVolume : MonoBehaviour
    {
        private void Reset()
        {
            var shelter = GetComponent<Collider>();
            if (shelter != null) shelter.isTrigger = true;
        }
    }
}
