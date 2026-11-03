using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevelPhysics;

[System.Serializable]
public class BoneSetting
{
    public BoneSetting(HumanBodyBones bone)
    {
        id = bone;
        string name = bone.ToString();
        if (name.Contains("Left")) name = name.Substring(4);
        this.name = name;
        thickness = 0.1f;
    }
    [HideInInspector]
    public string name = "New Bone";
    [HideInInspector]
    public HumanBodyBones id;
    [Range(0, 0.5f)] 
    public float thickness = 0.1f;
    public Vector3 offset1;
    public Vector3 offset2;
}

[ExecuteAlways]
public class BoneCollider : MonoBehaviour
{
    Dictionary<HumanBodyBones, HumanBodyBones> boneParent = new Dictionary<HumanBodyBones, HumanBodyBones>
    {
        { HumanBodyBones.Head, HumanBodyBones.Neck },
        { HumanBodyBones.Neck, HumanBodyBones.UpperChest },
        { HumanBodyBones.UpperChest, HumanBodyBones.Chest },
        { HumanBodyBones.Chest, HumanBodyBones.Spine },
        { HumanBodyBones.Spine, HumanBodyBones.Hips },
        { HumanBodyBones.LeftShoulder, HumanBodyBones.UpperChest },
        { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftShoulder },
        { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftUpperArm },
        { HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm },
        { HumanBodyBones.LeftUpperLeg, HumanBodyBones.Hips },
        { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftUpperLeg },
        { HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg },
        { HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot },

        { HumanBodyBones.RightShoulder, HumanBodyBones.UpperChest },
        { HumanBodyBones.RightUpperArm, HumanBodyBones.RightShoulder },
        { HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm },
        { HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm },
        { HumanBodyBones.RightUpperLeg, HumanBodyBones.Hips },
        { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg },
        { HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg },
        { HumanBodyBones.RightToes, HumanBodyBones.RightFoot },
    };

    private Animator animator;
    public Dictionary<HumanBodyBones, ClothPhysics.Capsule> humanBodyBones;
    public List<BoneSetting> boneSettings;

    private void BoneSettingsInit()
    {
        if (boneSettings != null)
            return;
        boneSettings = new List<BoneSetting>();
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            HumanBodyBones bone = (HumanBodyBones)i;
            string name = bone.ToString();
            if (!boneParent.ContainsKey(bone))
                continue;
            if (name.Contains("Right") || name.Contains("Hips"))
                continue;
            boneSettings.Add(new BoneSetting(bone));
        }
    }
    private void BoneUpdate()
    {
        humanBodyBones = new Dictionary<HumanBodyBones, ClothPhysics.Capsule>();
        foreach (BoneSetting bs in boneSettings)
        {
            HumanBodyBones bone = bs.id;
            Vector3 off1 = animator.GetBoneTransform(bone).TransformVector(bs.offset1 / 10.0f);
            Vector3 off2 = animator.GetBoneTransform(boneParent[bone]).TransformVector(bs.offset2 / 10.0f);
            ClothPhysics.Capsule boneCapsule;
            boneCapsule.start = animator.GetBoneTransform(bone).position + off1;
            boneCapsule.end = animator.GetBoneTransform(boneParent[bone]).position + off2;
            boneCapsule.radius = bs.thickness * transform.lossyScale.x;
            humanBodyBones.Add(bone, boneCapsule);
            if (bone.ToString().Contains("Left"))
            {
                bone++;
                off1 = animator.GetBoneTransform(bone).TransformVector(new Vector3(bs.offset1.x, bs.offset1.y, bs.offset1.z) / 10.0f);
                off2 = animator.GetBoneTransform(boneParent[bone]).TransformVector(new Vector3(bs.offset2.x, bs.offset2.y, bs.offset2.z) / 10.0f);
                boneCapsule.start = animator.GetBoneTransform(bone).position + off1;
                boneCapsule.end = animator.GetBoneTransform(boneParent[bone]).position + off2;
                boneCapsule.radius = bs.thickness * transform.lossyScale.x;
                humanBodyBones.Add(bone, boneCapsule);
            }

        }
    }
    private void OnEnable()
    {
        animator = GetComponent<Animator>();
        BoneSettingsInit();
        BoneUpdate();
    }

    void LateUpdate()
    {
        if(animator != null)
            BoneUpdate();
    }

    private void DrawCapsule(Vector3 start, Vector3 end, float radius, Transform boneTransform)
    {
        Vector3 topPointLocal = start;
        Vector3 bottomPointLocal = end;

        Vector3 topPointWorld = topPointLocal;
        Vector3 bottomPointWorld = bottomPointLocal;

        Gizmos.DrawWireSphere(topPointWorld, radius);
        Gizmos.DrawWireSphere(bottomPointWorld, radius);

        Vector3 right = boneTransform.right * radius;
        Vector3 forward = boneTransform.forward * radius;

        Gizmos.DrawLine(topPointWorld + right, bottomPointWorld + right);
        Gizmos.DrawLine(topPointWorld - right, bottomPointWorld - right);
        Gizmos.DrawLine(topPointWorld + forward, bottomPointWorld + forward);
        Gizmos.DrawLine(topPointWorld - forward, bottomPointWorld - forward);
    }

    public Color gizmoColor = Color.cyan;
    void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        foreach (var bone in humanBodyBones)
        {
            DrawCapsule(bone.Value.start, bone.Value.end, bone.Value.radius, animator.GetBoneTransform(bone.Key));
        }

    }

}
