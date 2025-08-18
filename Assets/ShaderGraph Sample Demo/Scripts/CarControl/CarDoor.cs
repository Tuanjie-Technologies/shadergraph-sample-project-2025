using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public enum DoorState
{
    Closed,
    Opening,
    Opened,
    Closing,
}

[Serializable]
public enum RotationAxis
{
    Up,
    Right,
    Forward,
    InvUp,
    InvRight,
    InvForward,
}

[Serializable]
public class Doors
{
    [HideInInspector]
    public float currentDegree;
    public DoorState DoorState;
    public GameObject DoorObject;

    private DoorState PreviousState;
    private Vector3 OriginalPosition;
    private Quaternion OriginalAngle;

    [Header("Hinges Settings")]
    public RotationAxis RotationAxis;

    [Range(0f,720f)]
    public float RotationAngle;

    public void InitOriginalValues()
    {
        if (DoorObject != null)
        {
            OriginalPosition = DoorObject.transform.localPosition;

            OriginalAngle = DoorObject.transform.localRotation;

            DoorState = DoorState.Closed;
            PreviousState = DoorState.Closed;
        }
    }

    public Vector3 GetOriginalPosition()
    {
        return OriginalPosition;
    }

    public Quaternion GetOriginalAngle()
    {
        return OriginalAngle;
    }

    public void SetCurrentStateSaved()
    {
        PreviousState = DoorState;
    }

    public DoorState GetPreviousDoorState()
    {
        return PreviousState;
    }

}

[RequireComponent(typeof(AudioSource))]
public class CarDoor : MonoBehaviour
{
    [Range(0.25f, 120f)]
    public float DoorSpeed = 45f;
    public List<Doors> DoorList = new List<Doors>();
    float aoLerp = 0f;
    DoorState doorState = DoorState.Closed;
    DoorState previousDoorState = DoorState.Closed;
    public AudioClip doorOpenSound;
    public AudioClip doorCloseSound;
    AudioSource audioSource;

    void Start()
    {
        for (int i = 0; i < DoorList.Count; i++)
        {
            DoorList[i].InitOriginalValues();
        }
        audioSource = GetComponent<AudioSource>();
    }

    public void ToggleDoorControl(int doorIndex)
    {
        if (doorIndex < DoorList.Count && doorIndex >= 0)
        {
            switch (DoorList[doorIndex].DoorState)
            {
                case DoorState.Closed:
                    DoorList[doorIndex].DoorState = DoorState.Opening;
                    doorState = DoorState.Opening;
                    if (doorOpenSound != null)
                    {
                        audioSource.clip = doorOpenSound;
                        audioSource.Play();
                    }
                    break;
                case DoorState.Opened:
                    DoorList[doorIndex].DoorState = DoorState.Closing;
                    doorState = DoorState.Closing;
                    break;
                case DoorState.Closing:
                    DoorList[doorIndex].DoorState = DoorState.Opening;
                    doorState = DoorState.Opening;
                    break;
                case DoorState.Opening:
                    DoorList[doorIndex].DoorState = DoorState.Closing;
                    doorState = DoorState.Closing;
                    break;
            }

        }
    }

    void Update()
    {
        foreach (Doors curDoor in DoorList)
        {
            var rotationAxis = curDoor.DoorObject.transform.up;
            switch (curDoor.RotationAxis)
            {
                case RotationAxis.Up:
                    rotationAxis = curDoor.DoorObject.transform.up;
                    break;
                case RotationAxis.Right:
                    rotationAxis = curDoor.DoorObject.transform.right;
                    break;
                case RotationAxis.Forward:
                    rotationAxis = curDoor.DoorObject.transform.forward;
                    break;
                case RotationAxis.InvUp:
                    rotationAxis = -curDoor.DoorObject.transform.up;
                    break;
                case RotationAxis.InvRight:
                    rotationAxis = -curDoor.DoorObject.transform.right;
                    break;
                case RotationAxis.InvForward:
                    rotationAxis = -curDoor.DoorObject.transform.forward;
                    break;
            }

            DoorSpeed = curDoor.RotationAngle;

            if (curDoor.DoorObject != null)
            {

                switch (curDoor.DoorState)
                {
                    case DoorState.Closed:
                        curDoor.DoorObject.transform.localRotation = curDoor.GetOriginalAngle();
                        break;

                    case DoorState.Opened:
                        break;

                    case DoorState.Opening:
                        if (curDoor.GetPreviousDoorState() == DoorState.Opened)
                            break;

                        curDoor.currentDegree += DoorSpeed * Time.deltaTime;

                        if (curDoor.currentDegree < curDoor.RotationAngle)
                        {
                            curDoor.DoorObject.transform.RotateAround(curDoor.DoorObject.transform.position, rotationAxis, DoorSpeed * Time.deltaTime);
                        }
                        else
                        {
                            curDoor.DoorState = DoorState.Opened;
                            curDoor.SetCurrentStateSaved();
                            curDoor.currentDegree = 0f;
                        }

                        break;

                    case DoorState.Closing:
                        if (curDoor.GetPreviousDoorState() == DoorState.Closed)
                            break;

                        curDoor.currentDegree += DoorSpeed * Time.deltaTime;

                        if (curDoor.currentDegree < curDoor.RotationAngle)
                        {
                            curDoor.DoorObject.transform.RotateAround(curDoor.DoorObject.transform.position, -rotationAxis, DoorSpeed * Time.deltaTime);
                        }
                        else
                        {
                            curDoor.DoorState = DoorState.Closed;
                            if (doorCloseSound != null)
                            {
                                audioSource.clip = doorCloseSound;
                                audioSource.Play();
                            }
                            curDoor.SetCurrentStateSaved();
                            curDoor.currentDegree = 0f;
                        }

                        break;
                }

            }
        }

        switch (doorState)
        {
            case DoorState.Closed:
                Shader.SetGlobalFloat("_AOLerp", 0f);
                break;

            case DoorState.Opened:
                Shader.SetGlobalFloat("_AOLerp", 1f);
                break;

            case DoorState.Opening:
                if (previousDoorState == DoorState.Opened)
                    break;

                aoLerp += Time.deltaTime;

                if (aoLerp < 1f)
                {
                    Shader.SetGlobalFloat("_AOLerp", aoLerp);
                }
                else
                {
                    doorState = DoorState.Opened;
                    previousDoorState = doorState;
                }

                break;

            case DoorState.Closing:
                if (previousDoorState == DoorState.Closed)
                    break;

                aoLerp -= Time.deltaTime;

                if (aoLerp > 0f)
                {
                    Shader.SetGlobalFloat("_AOLerp", aoLerp);
                }
                else
                {
                    doorState = DoorState.Closed;
                    previousDoorState = doorState;
                }

                break;
        }
    }

    void OnApplicationQuit() => Shader.SetGlobalFloat("_AOLerp", 0f);

}
