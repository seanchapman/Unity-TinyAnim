using UnityEngine;
using System.Collections;
using System;

public class CameraTinyAnim : MonoBehaviour
{

    // The transform component, needed for manual animation.
    Transform m_transform;

    // The TinyAnim class for this game object (the camera)
    TinyAnim m_animHelper = new TinyAnim();

	void Start ()
    {
        // Get the transform component so we can animate the camera manually
        m_transform = GetComponent<Transform>();

        // Add our SmoothStep keyframes
        m_animHelper.AddKeyframe(new TinyKeyframe(new Vector3(0f, 0f, 0f), TimeSpan.FromSeconds(0), true));
        m_animHelper.AddKeyframe(new TinyKeyframe(new Vector3(0f, 2.5f, -8f), TimeSpan.FromSeconds(4), true));
        m_animHelper.AddKeyframe(new TinyKeyframe(new Vector3(8f, 2.5f, -8f), TimeSpan.FromSeconds(7), true));
        m_animHelper.AddKeyframe(new TinyKeyframe(new Vector3(-8f, 2.5f, -8f), TimeSpan.FromSeconds(10), true));
        m_animHelper.AddKeyframe(new TinyKeyframe(new Vector3(0f, 2.5f, -8f), TimeSpan.FromSeconds(12), true));

        // Add other non-smooth keyframes for performance testing
        for (int i = 0; i < 19990; ++i)
        {
            float val = 0f - ((float)i * 0.01f) - 8f;
            double time = ((double)i * 0.001) + 12.0;
            m_animHelper.AddKeyframe(new TinyKeyframe(new Vector3(0f, 2.5f, val), TimeSpan.FromSeconds(time)));
        }
        m_animHelper.SortArray();
    }

    void Update ()
    {
        // Set the transform from the TinyAnim class
        m_transform.position = m_animHelper.GetValue(TimeSpan.FromSeconds(Time.time));
	}
}
