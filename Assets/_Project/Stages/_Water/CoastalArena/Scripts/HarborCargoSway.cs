using UnityEngine;
/// <summary>Small suspended cargo; one rigid transform, no cloth or physics simulation.</summary>
[ExecuteAlways,DisallowMultipleComponent]
public sealed class HarborCargoSway:MonoBehaviour
{
 [Range(0,5)] public float swingDegrees=1.4f;
 [Min(.01f)] public float swingSpeed=.62f;
 public float phase=1.3f;
 void Update(){float t=Application.isPlaying?Time.time:Time.realtimeSinceStartup;
 transform.localRotation=Quaternion.Euler(Mathf.Sin(t*swingSpeed+phase)*swingDegrees*.55f,0,Mathf.Sin(t*swingSpeed*.83f+phase)*swingDegrees);}
 void OnDisable(){transform.localRotation=Quaternion.identity;}
}