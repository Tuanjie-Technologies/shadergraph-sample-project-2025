using Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;

public class CameraControl : MonoBehaviour
{
    public CinemachineFreeLook freeLookCamera;
    public CinemachineVirtualCamera interiorCamera;
    public PlayableDirector cinematicTimeline;

    public CinemachineBrain brain;
    public Canvas canvas;

    bool freeCamFadeIn = false;

    // Start is called before the first frame update
    void Start()
    {
        if (!freeLookCamera || !interiorCamera || !cinematicTimeline || !brain || !canvas)
            return;

        interiorCamera.enabled = false;
        freeCamFadeIn = false;
#if UNITY_EDITOR
        // cinematicTimeline.enabled = false;
#endif
        canvas.enabled = !cinematicTimeline.enabled;
        cinematicTimeline.Play();
        cinematicTimeline.stopped += OnCinematicEnd;
    }

    // Update is called once per frame
    void Update()
    {
        if (!freeLookCamera || !interiorCamera || !cinematicTimeline || !brain || !canvas)
            return;

        freeLookCamera.m_YAxis.m_MaxSpeed = freeLookCamera.m_XAxis.m_MaxSpeed = Input.GetMouseButton(0) && brain.ActiveVirtualCamera.Name.Equals(freeLookCamera.Name) ? 2f : 0f;

        if (freeCamFadeIn)
        {
            CinemachineStoryboard storyboard = freeLookCamera.GetComponent<CinemachineStoryboard>();
            if (storyboard != null)
            {
                storyboard.enabled = true;
                storyboard.m_Alpha -= Time.deltaTime * 0.5f;
                storyboard.m_Alpha = Mathf.Max(0f, storyboard.m_Alpha);
                if (storyboard.m_Alpha <= 0f)
                {
                    freeCamFadeIn = false;
                    storyboard.enabled = false;
                    storyboard.m_Alpha = 1f;
                }    
            }
        }
        else
        {
            CinemachineStoryboard storyboard = freeLookCamera.GetComponent<CinemachineStoryboard>();
            storyboard.enabled = false;
            storyboard.m_Alpha = 1f;
        }
    }

    public void ToggleInteriorCamera()
    {
        interiorCamera.enabled = !interiorCamera.enabled;
        freeCamFadeIn = false;
    }

    public void OnFreeCameraLive() => freeCamFadeIn = true;
    void OnCinematicEnd(PlayableDirector playableDirector)
    {
        if (!freeLookCamera || !interiorCamera || !cinematicTimeline || !brain || !canvas)
            return;

        cinematicTimeline.enabled = false;
        canvas.enabled = true;
        OnFreeCameraLive();
    }

    void OnDisable() => cinematicTimeline.stopped -= OnCinematicEnd;
}
