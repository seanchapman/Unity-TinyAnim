//
// TinyAnim
// Written by Sean Chapman
// www.seanchapman.co.uk
// 
// A class to handle keyframe animation with linear interpolation via percentage and SmoothStep blending.
// The class was deliberately designed to only use a basic C# array and not to use any animation methods from Unity.
//

// A note on complexity: The algorithms I implemented are fairly simple.
// The keyframes are iterated over in pairs, moving through the array which is in chronological order.
// When at the start and end of the array, we check to see if the current time is outside the range of our keyframes, in order to return either
// the first or last keyframe in the list. If the current time is inside our range of keyframes for the pair we are currently looking at, the 
// interpolation is performed as configured in the keyframe pair.

using UnityEngine;
using System.Collections;
using System;

// TinyKeyframe class for TinyAnim
// Defines a Keyframe as Vector3 at a given time, accurate to the nearest tick.
// Also defined is wether the keyframe should be smoothed with SmoothStep blending or not.
public class TinyKeyframe : IComparable
{
    // The XYZ values of the keyframe
    public Vector3 vec;

    // The time of the keyframe, which is also the unique identifier.
    // We are using TimeSpan here rather than float to avoid floating point inaccuracies.
    public TimeSpan time;

    // This boolean indicates wether SmoothStep blending should be used for this keyframe.
    // If this is false then percentage blend is used.
    public bool smooth;

    public TinyKeyframe(Vector3 vec, TimeSpan time, bool smooth = false)
    {
        this.vec = vec;
        this.time = time;
        this.smooth = smooth;
    }

    // Comparison for Array.Sort
    public int CompareTo(object other)
    {
        // Send all null keyframes to the back of the array
        if (other == null)
            return -1;

        // Sort actual keyframes
        TinyKeyframe otherKf = other as TinyKeyframe;
        if (otherKf != null)
        {
            return time.CompareTo(otherKf.time);
        }
        else
        {
            throw new ArgumentException("Comparison object is not a TinyKeyframe!");
        }
    }
}

// The TinyAnim class, which handles management of the keyframe array and interpolation.
// Supports linear interpolation with percentage and SmoothStep blending.
// Supports a 'realistic' keyframe count of around 6000 per TinyAnim instance (typically per GameObject).
// This is due to the fact that the array is sorted after every keyframe is added and this gets exponentially more time consuming.
// But this is intentional to allow the programmer to add keyframes on-the-fly. It is possible to only sort after all keyframes have been added, 
// which does greatly improve performance, but this then sacrifices usability.
public class TinyAnim
{
    // Max number of keyframes
    private const int m_maxKeyframes = 20000;

    // The keyframe array
    private TinyKeyframe[] m_keyframes = new TinyKeyframe[m_maxKeyframes];

    // The index of the next free keyframe in the array. Also doubles as the number of keyframes in the array.
    private int m_nextFreeIndex = 0;

    // Add (or update) a keyframe to the array. This also sorts the array so that the keyframes remain in ascending time order.
    // If a keyframe already exists at the given time, update that keyframe instead.
    public void AddKeyframe(TinyKeyframe kf)
    {
        // Ensure keyframe array is not full
        if (m_nextFreeIndex < m_maxKeyframes)
        {
            // Does a keyframe already exists at the given time?
            TinyKeyframe existing = Array.Find(m_keyframes, o => o != null ? o.time.Equals(kf.time) : false);
            if (existing != null)
            {
                // Update the existing keyframe
                existing.vec = kf.vec;
            }
            else
            {
                // Add new keyframe
                m_keyframes[m_nextFreeIndex] = kf;
                m_nextFreeIndex++;
            }
        }
        else
        {
            throw new OverflowException("The keyframe array is full.");
        }
    }

    // Removes a keyframe from the array at a given time.
    // Will also sort the array if necessary.
    public void RemoveKeyframe(TimeSpan time)
    {
        // Find the keyframe in the array
        int existingIndex = Array.FindIndex(m_keyframes, o => o != null ? o.time.Equals(time) : false);
        if (existingIndex != -1)
        {
            m_keyframes[existingIndex] = null;

            // Resort the array if we have removed any except the last in the list
            if (existingIndex < m_nextFreeIndex-1)
            {
                SortArray();
            }

            m_nextFreeIndex--;
        }
    }

    // Resize the keyframe array
    public void SetMaxKeyframes(int newMax)
    {
        Array.Resize(ref m_keyframes, newMax);
    }

    // Sort keyframes in ascending time order
    public void SortArray()
    {
        // Sort keyframes and shuffle nulls to the end of the array
        Array.Sort(m_keyframes, (a, b) => a == null ? 1 : b == null ? -1 : a.CompareTo(b));
    }

    // Get the Vector3 value for the given time
    public Vector3 GetValue(TimeSpan time)
    {
        // Check sizes
        if (m_nextFreeIndex == 0)
        {
            throw new Exception("Attempted to get value from TinyAnim but no keyframes had been added to the array yet!");
        }
        else if (m_nextFreeIndex == 1)
        {
            return m_keyframes[0].vec;
        }
        else if (m_nextFreeIndex > 1)
        {
            int index = Array.BinarySearch(m_keyframes, 0, m_nextFreeIndex, new TinyKeyframe(new Vector3(0,0,0), time));
            if (index < 0)
            {
                // Not an exact match so fetch the closest index using bitwise complement
                index = ~index;

                if (index == 0)
                {
                    // Time < first keyframe time - Return first keyframe's value
                    return m_keyframes[0].vec;
                }
                else if (index >= m_nextFreeIndex)
                {
                    // Time > last keyframe time - Return last keyframe's value
                    return m_keyframes[m_nextFreeIndex - 1].vec;
                }
                else
                {
                    // Index will be the end boundary keyframe
                    TinyKeyframe startBound = m_keyframes[index - 1];
                    TinyKeyframe endBound = m_keyframes[index];
                    if (startBound != null && endBound != null)
                    {
                        // We have found the correct two keyframes that we need to interpolate between
                        bool smooth = startBound.smooth && endBound.smooth;
                        return TinyLerp(startBound, endBound, time, smooth);
                    }
                }
            }
            else
            {
                // Exact match - return that keyframe's value
                return m_keyframes[index].vec;
            }
        }

        // This should never happen but it is here to catch anomolies and satisfy the compiler
        throw new Exception("Attempted to get value from TinyAnim but no Vector was returned!");
    }

    // Interpolate between two TinyKeyframes
    private Vector3 TinyLerp(TinyKeyframe start, TinyKeyframe end, TimeSpan time, bool smoothStep)
    {
        // Calculate blend value depending on what is chosen (percentage or smooth step)
        float percent = 0f;
        if (smoothStep)
        {
            percent = SmoothStep(time, start.time, end.time);
        }
        else
        {
            percent = Percent(time, start.time, end.time);
        }

        return new Vector3(start.vec.x + (percent * (end.vec.x - start.vec.x)),
                           start.vec.y + (percent * (end.vec.y - start.vec.y)),
                           start.vec.z + (percent * (end.vec.z - start.vec.z)));
    }

    // Calculate smooth step blend value of the current given time between a start and end time
    private float SmoothStep(TimeSpan current, TimeSpan start, TimeSpan end)
    {
        float tCurrent = Clamp(0, 1, Percent(current, start, end));

        // Smooth the percentage with the polynomial formula
        return (float)Math.Pow(tCurrent, 2) * (3 - (2 * tCurrent));
    }

    // Calculate percentage blend value of the current given time between a start and end time
    private float Percent(TimeSpan current, TimeSpan start, TimeSpan end)
    {
        float tCurrent = (float)current.Ticks;
        float tStart = (float)start.Ticks;
        float tEnd = (float)end.Ticks;
        return (tCurrent - tStart) / (tEnd - tStart);
    }

    // Ensures a given value stays between two boundaries
    private float Clamp(float value, float startValue, float endValue)
    {
        value = Math.Min(value, Math.Max(startValue, endValue));
        value = Math.Max(value, Math.Min(startValue, endValue));
        return value;
    }
}
