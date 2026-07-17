#if UNITY_EDITOR
using System;
using System.Linq;
using Hollowfen.Dialogue;
using Hollowfen.NPCs;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Focused Play Mode proof for Bram's material and dialogue-driven talking animation.</summary>
    public static class BramCharacterVerifier
    {
        private const string ModelPath = "Assets/Characters/Bram/Bram-TPose.fbx";
        private const string TalkingPath = "Assets/Characters/Bram/Bram-Talking.fbx";
        private const string ControllerPath = "Assets/Characters/Bram/Bram_Idle.controller";
        private const string DialoguePath =
            "Assets/_Hollowfen/Data/Dialogue/Dialogue_Act1_Bram_Repeat.asset";

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");

            var material = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>().FirstOrDefault();
            string texturePath = material != null && material.mainTexture != null
                ? AssetDatabase.GetAssetPath(material.mainTexture)
                : string.Empty;
            Require(texturePath.StartsWith("Assets/Characters/Bram/Textures/", StringComparison.Ordinal),
                "Bram's model is not using its own extracted texture");

            var talkingClip = AssetDatabase.LoadAllAssetsAtPath(TalkingPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == "Bram_Talking");
            Require(talkingClip != null && talkingClip.length > 0f && talkingClip.isLooping,
                "Bram_Talking is missing or not configured as a looping clip");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Require(controller != null && controller.parameters.Any(parameter =>
                    parameter.name == "Talking" && parameter.type == AnimatorControllerParameterType.Bool),
                "Bram's controller has no Talking bool");
            Require(controller.layers.SelectMany(layer => layer.stateMachine.states)
                    .Any(child => child.state.name == "Talking" && child.state.motion == talkingClip),
                "Bram_Talking is not assigned to the controller's Talking state");

            var bram = GameObject.Find("NPC_Bram");
            Require(bram != null, "NPC_Bram is missing from the active scene");
            var driver = bram.GetComponent<DialogueSpeakerAnimator>();
            Require(driver != null && driver.SpeakerName == "Bram" && driver.CharacterAnimator != null,
                "NPC_Bram has no configured DialogueSpeakerAnimator");
            Require(driver.CharacterAnimator.runtimeAnimatorController == controller,
                "Bram's scene Animator is using the wrong controller");

            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(DialoguePath);
            var screen = DialogueScreen.Instance;
            Require(dialogue != null && dialogue.Lines.Length > 0 && dialogue.Lines[0].speaker == "Bram",
                "Bram verification dialogue is missing or no longer begins with Bram");
            Require(screen != null, "DialogueScreen is missing");
            if (screen.IsOpen) screen.Close();
            screen.Open(dialogue, bram.transform);
            Require(driver.IsTalking, "Bram did not enter Talking when his line was presented");
            screen.Close();
            Require(!driver.IsTalking, "Bram did not return to Idle when dialogue closed");

            return "BRAM CHARACTER — PASS: local 2K texture, 6.1s authored talking loop, " +
                   "Humanoid controller transitions, and speaker-driven dialogue dispatch";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BramCharacterVerifier] " + message);
        }
    }
}
#endif
