using UnityEngine;

namespace Zombio
{
    /// <summary>Short pooled-material primitive effect, cleaned with the wave runtime root.</summary>
    public sealed class EnemyBurst : MonoBehaviour
    {
        private readonly Transform[] _shards = new Transform[7];
        private readonly Vector3[] _velocities = new Vector3[7];
        private float _age;
        private float _radius;

        public void Configure(Color color, float radius)
        {
            _radius = radius;
            for (int i = 0; i < _shards.Length; i++)
            {
                _shards[i] = EnemyVisuals.Primitive(PrimitiveType.Cube, transform, "Particle", Vector3.zero, Vector3.one * Mathf.Min(0.3f, radius * 0.3f), color, true);
                _velocities[i] = (Random.onUnitSphere + Vector3.up * 0.5f) * radius * 3f;
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            for (int i = 0; i < _shards.Length; i++)
            {
                if (_shards[i] == null) continue;
                _shards[i].localPosition += _velocities[i] * Time.deltaTime;
                _velocities[i] += Vector3.down * 7f * Time.deltaTime;
                _shards[i].Rotate(new Vector3(120f, 90f, 35f) * Time.deltaTime);
                _shards[i].localScale = Vector3.one * Mathf.Min(0.3f, _radius * 0.3f) * Mathf.Clamp01(1f - _age / 0.65f);
            }
            if (_age >= 0.65f) Destroy(gameObject);
        }
    }
}
