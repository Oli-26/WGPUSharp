using System.Numerics;
using System.Runtime.InteropServices;

namespace WgpuSharp.Scene;

/// <summary>A single joint in a skeleton hierarchy.</summary>
public sealed class Joint
{
    /// <summary>Index of this joint in the skeleton's joint array.</summary>
    public int Index { get; init; }
    /// <summary>Name of the joint (from glTF node name).</summary>
    public string Name { get; init; } = "";
    /// <summary>Index of the parent joint, or -1 if root.</summary>
    public int ParentIndex { get; init; } = -1;
    /// <summary>Rest-pose local translation.</summary>
    public Vector3 Translation { get; init; }
    /// <summary>Rest-pose local rotation.</summary>
    public Quaternion Rotation { get; init; } = Quaternion.Identity;
    /// <summary>Rest-pose local scale.</summary>
    public Vector3 Scale { get; init; } = Vector3.One;
}

/// <summary>Skeleton: topologically-sorted joint hierarchy + inverse bind matrices.</summary>
public sealed class Skeleton
{
    /// <summary>All joints, ordered so that parents always precede children.</summary>
    public Joint[] Joints { get; init; } = [];
    /// <summary>Inverse bind matrices, one per joint.</summary>
    public Matrix4x4[] InverseBindMatrices { get; init; } = [];
    /// <summary>Number of joints.</summary>
    public int JointCount => Joints.Length;
}

/// <summary>Interpolation mode for animation keyframes.</summary>
public enum AnimationInterpolation { Linear, Step, CubicSpline }

/// <summary>Which transform component an animation channel targets.</summary>
public enum AnimationPath { Translation, Rotation, Scale }

/// <summary>An animation channel targeting one joint's TRS property.</summary>
public sealed class AnimationChannel
{
    /// <summary>Index of the target joint in the skeleton.</summary>
    public int JointIndex { get; init; }
    /// <summary>Which property is animated.</summary>
    public AnimationPath Path { get; init; }
    /// <summary>Keyframe timestamps in seconds.</summary>
    public float[] Times { get; init; } = [];
    /// <summary>
    /// Keyframe values. Translation/Scale: 3 floats per key. Rotation: 4 floats per key (xyzw quaternion).
    /// </summary>
    public float[] Values { get; init; } = [];
    /// <summary>Interpolation mode.</summary>
    public AnimationInterpolation Interpolation { get; init; } = AnimationInterpolation.Linear;
}

/// <summary>A named animation clip containing one or more channels.</summary>
public sealed class AnimationClip
{
    /// <summary>Name of the clip (from glTF).</summary>
    public string Name { get; init; } = "";
    /// <summary>Duration in seconds (max timestamp across all channels).</summary>
    public float Duration { get; init; }
    /// <summary>All channels in this clip.</summary>
    public AnimationChannel[] Channels { get; init; } = [];
}

/// <summary>
/// Per-node animation playback state. Samples keyframes and computes
/// the final joint matrix array ready for GPU upload each frame.
/// </summary>
public sealed class AnimationPlayer
{
    private readonly Skeleton _skeleton;
    private readonly AnimationClip[] _clips;

    // Pre-allocated working arrays (zero-alloc per frame)
    private readonly Vector3[] _localTranslations;
    private readonly Quaternion[] _localRotations;
    private readonly Vector3[] _localScales;
    private readonly Matrix4x4[] _worldMatrices;
    private readonly Matrix4x4[] _jointMatrices;
    private readonly byte[] _jointMatrixBytes;

    public AnimationPlayer(Skeleton skeleton, AnimationClip[] clips)
    {
        _skeleton = skeleton;
        _clips = clips;
        int n = skeleton.JointCount;
        _localTranslations = new Vector3[n];
        _localRotations = new Quaternion[n];
        _localScales = new Vector3[n];
        _worldMatrices = new Matrix4x4[n];
        _jointMatrices = new Matrix4x4[n];
        // Ensure at least 256 bytes for WebGPU storage buffer alignment
        _jointMatrixBytes = new byte[Math.Max(256, n * 64)];

        // Initialize rest pose
        for (int i = 0; i < n; i++)
        {
            _localTranslations[i] = skeleton.Joints[i].Translation;
            _localRotations[i] = skeleton.Joints[i].Rotation;
            _localScales[i] = skeleton.Joints[i].Scale;
        }

        // Compute initial joint matrices (rest pose)
        ComputeJointMatrices();
    }

    /// <summary>The skeleton this player animates.</summary>
    public Skeleton Skeleton => _skeleton;
    /// <summary>Available animation clips.</summary>
    public AnimationClip[] Clips => _clips;
    /// <summary>Index of the currently active clip.</summary>
    public int CurrentClipIndex { get; set; }
    /// <summary>The currently active clip, or null if no clips.</summary>
    public AnimationClip? CurrentClip => _clips.Length > 0 && CurrentClipIndex < _clips.Length ? _clips[CurrentClipIndex] : null;
    /// <summary>Current playback time in seconds.</summary>
    public float Time { get; set; }
    /// <summary>Whether the animation is currently playing.</summary>
    public bool IsPlaying { get; set; }
    /// <summary>Whether to loop the animation.</summary>
    public bool Loop { get; set; } = true;
    /// <summary>Playback speed multiplier.</summary>
    public float Speed { get; set; } = 1f;
    /// <summary>Pre-computed joint matrix bytes for GPU upload (64 bytes per joint).</summary>
    public byte[] JointMatrixBytes => _jointMatrixBytes;

    /// <summary>Start or resume playback.</summary>
    public void Play() => IsPlaying = true;
    /// <summary>Pause playback.</summary>
    public void Pause() => IsPlaying = false;
    /// <summary>Stop playback and reset to the beginning.</summary>
    public void Stop() { IsPlaying = false; Time = 0; ComputeJointMatrices(); }
    /// <summary>Switch to a clip by index.</summary>
    public void SetClip(int index) { CurrentClipIndex = Math.Clamp(index, 0, Math.Max(0, _clips.Length - 1)); Time = 0; }

    /// <summary>
    /// Advance animation time and recompute joint matrices.
    /// Call once per frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        var clip = CurrentClip;
        if (clip is null || clip.Duration <= 0) return;

        if (IsPlaying)
        {
            Time += deltaTime * Speed;
            if (Loop)
                Time %= clip.Duration;
            else
                Time = MathF.Min(Time, clip.Duration);
        }

        // Reset to rest pose
        for (int i = 0; i < _skeleton.JointCount; i++)
        {
            _localTranslations[i] = _skeleton.Joints[i].Translation;
            _localRotations[i] = _skeleton.Joints[i].Rotation;
            _localScales[i] = _skeleton.Joints[i].Scale;
        }

        // Sample each channel at current time
        foreach (var channel in clip.Channels)
        {
            if (channel.JointIndex < 0 || channel.JointIndex >= _skeleton.JointCount) continue;
            if (channel.Times.Length == 0) continue;

            int j = channel.JointIndex;
            switch (channel.Path)
            {
                case AnimationPath.Translation:
                    _localTranslations[j] = SampleVec3(channel);
                    break;
                case AnimationPath.Rotation:
                    _localRotations[j] = SampleQuat(channel);
                    break;
                case AnimationPath.Scale:
                    _localScales[j] = SampleVec3(channel);
                    break;
            }
        }

        ComputeJointMatrices();
    }

    private void ComputeJointMatrices()
    {
        // Forward kinematics: parents guaranteed before children (topological order)
        for (int i = 0; i < _skeleton.JointCount; i++)
        {
            var local = Matrix4x4.CreateScale(_localScales[i])
                      * Matrix4x4.CreateFromQuaternion(_localRotations[i])
                      * Matrix4x4.CreateTranslation(_localTranslations[i]);

            int parent = _skeleton.Joints[i].ParentIndex;
            _worldMatrices[i] = parent >= 0 ? local * _worldMatrices[parent] : local;
            _jointMatrices[i] = _skeleton.InverseBindMatrices[i] * _worldMatrices[i];
        }

        // Serialize to bytes for GPU upload
        MemoryMarshal.AsBytes(_jointMatrices.AsSpan()).CopyTo(_jointMatrixBytes);
    }

    private Vector3 SampleVec3(AnimationChannel channel)
    {
        float t = Time;
        var times = channel.Times;
        var values = channel.Values;

        if (t <= times[0])
            return new Vector3(values[0], values[1], values[2]);
        if (t >= times[^1])
        {
            int last = (times.Length - 1) * 3;
            return new Vector3(values[last], values[last + 1], values[last + 2]);
        }

        int idx = FindKeyframe(times, t);
        float t0 = times[idx], t1 = times[idx + 1];
        float f = (t - t0) / (t1 - t0);

        if (channel.Interpolation == AnimationInterpolation.Step)
            f = 0;

        int i0 = idx * 3, i1 = (idx + 1) * 3;
        return Vector3.Lerp(
            new Vector3(values[i0], values[i0 + 1], values[i0 + 2]),
            new Vector3(values[i1], values[i1 + 1], values[i1 + 2]),
            f);
    }

    private Quaternion SampleQuat(AnimationChannel channel)
    {
        float t = Time;
        var times = channel.Times;
        var values = channel.Values;

        if (t <= times[0])
            return new Quaternion(values[0], values[1], values[2], values[3]);
        if (t >= times[^1])
        {
            int last = (times.Length - 1) * 4;
            return new Quaternion(values[last], values[last + 1], values[last + 2], values[last + 3]);
        }

        int idx = FindKeyframe(times, t);
        float t0 = times[idx], t1 = times[idx + 1];
        float f = (t - t0) / (t1 - t0);

        if (channel.Interpolation == AnimationInterpolation.Step)
            f = 0;

        int i0 = idx * 4, i1 = (idx + 1) * 4;
        return Quaternion.Slerp(
            new Quaternion(values[i0], values[i0 + 1], values[i0 + 2], values[i0 + 3]),
            new Quaternion(values[i1], values[i1 + 1], values[i1 + 2], values[i1 + 3]),
            f);
    }

    /// <summary>Binary search for the keyframe index such that times[idx] &lt;= t &lt; times[idx+1].</summary>
    private static int FindKeyframe(float[] times, float t)
    {
        int lo = 0, hi = times.Length - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (times[mid] <= t)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo;
    }
}
