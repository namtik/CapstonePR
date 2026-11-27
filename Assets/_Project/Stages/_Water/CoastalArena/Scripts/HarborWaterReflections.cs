using UnityEngine;
[ExecuteAlways,DisallowMultipleComponent]
public sealed class HarborWaterReflections:MonoBehaviour {
 public StageLightingManager lighting;
 public Renderer water;
 public ReflectionProbe dockReflectionProbe;
 public Cubemap[] presetSkies=new Cubemap[8];
 public Texture2D[] presetPierReflections=new Texture2D[8];
 public Matrix4x4 reflectionViewProjection=Matrix4x4.identity;
 public Vector3 reflectionCameraPosition=new Vector3(0,1,-9.5f);
 MaterialPropertyBlock block;
 int applied=-1;
 void OnEnable(){applied=-1;Refresh();}
 void OnValidate(){applied=-1;Refresh();}
 void Update(){if(lighting&&(int)lighting.CurrentTime!=applied)Refresh();}
 public void Refresh(){
  if(!water||!lighting||presetSkies==null)return;
  int index=(int)lighting.CurrentTime;
  if(index<0||index>=presetSkies.Length||!presetSkies[index])return;
  if(dockReflectionProbe&&dockReflectionProbe.customBakedTexture!=presetSkies[index])dockReflectionProbe.customBakedTexture=presetSkies[index];
  if(block==null)block=new MaterialPropertyBlock();
  var pier=presetPierReflections!=null&&index<presetPierReflections.Length?presetPierReflections[index]:null;
  water.GetPropertyBlock(block);
  block.SetTexture("_SkyCube",presetSkies[index]);
  block.SetTexture("_StageReflection",pier?pier:Texture2D.blackTexture);
  block.SetFloat("_StageReflectionAvailable",pier?1:0);
  block.SetMatrix("_StageReflectionVP",reflectionViewProjection);
  block.SetVector("_ReflectionCameraPosition",reflectionCameraPosition);
  water.SetPropertyBlock(block);applied=index;
 }
 void OnDisable(){
  if(!water||block==null)return;
  var fallback=water.sharedMaterial?water.sharedMaterial.GetTexture("_SkyCube"):null;
  if(fallback){water.GetPropertyBlock(block);block.SetTexture("_SkyCube",fallback);
   block.SetFloat("_StageReflectionAvailable",0);water.SetPropertyBlock(block);}
  applied=-1;
 }
}