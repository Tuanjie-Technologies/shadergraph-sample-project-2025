using System.Collections.Generic;
using UnityEngine;

public class CarLight : MonoBehaviour
{
    [ColorUsage(hdr: true, showAlpha: true)] public Color LightEmissiveColor = Color.white;
    public List<GameObject> PartsForLight = new List<GameObject>();

    private bool lightOn = true;
    private List<Renderer> CarLightRenderers = new List<Renderer>();

    void Start()
    {
        lightOn = true;
        RefreshObjects();
        SetMaterialsForLight();
    }

    public void RefreshObjects()
    {
        foreach(GameObject go in PartsForLight)
        {
            if (go == null)
                continue;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();

            foreach(Renderer render in renderers)
            {
                if (!CarLightRenderers.Contains(render))
                    CarLightRenderers.Add(render);
            }
        }
    }

    private void SetMaterialsForLight()
    {
        foreach(Renderer rend in CarLightRenderers)
        {
            if (rend == null)
                continue;

            MaterialPropertyBlock mBlock = new MaterialPropertyBlock();

            if (!lightOn)
            {
                mBlock.SetColor("_Emission", LightEmissiveColor);
                rend.SetPropertyBlock(mBlock);
            }
            else
            {
                rend.SetPropertyBlock(null);
            }
        }
    }

    public void ToogleLights()
    {
        lightOn = !lightOn;
        SetMaterialsForLight();
    }

}
