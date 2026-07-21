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
            "Assets/_Hollowfen/Data/Dialogue/Dialogue_Act1_MarraKitchen_FirstBasket.asset";

        [MenuItem("Hollowfen/Verify/Bram Character + Marra Conversation")]
        private static void RunFromMenu() => Debug.Log(RunAll());

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

            var marra = GameObject.Find("NPC_Marra");
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(DialoguePath);
            var screen = DialogueScreen.Instance;
            Require(dialogue != null && dialogue.Lines.Length > 0 && dialogue.Lines[0].speaker == "Bram",
                "Marra's first conversation is missing or no longer begins with Bram");
            Require(marra != null, "NPC_Marra is missing from the active scene");
            Require(screen != null, "DialogueScreen is missing");

            Vector3 originalPosition = bram.transform.position;
            Quaternion originalRotation = bram.transform.rotation;
            bool originallyActive = bram.activeSelf;
            try
            {
                if (!originallyActive) bram.SetActive(true);
                bram.transform.position = marra.transform.position + marra.transform.right * 2.4f;
                bram.transform.rotation = Quaternion.LookRotation(marra.transform.position - bram.transform.position,
                    Vector3.up);
                Require(bram.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled),
                    "Bram has no enabled renderer for the Marra conversation");

                if (screen.IsOpen) screen.Close();
                screen.Open(dialogue, marra.transform);
                Require(DialogueCinematics.Instance != null &&
                        DialogueCinematics.Instance.CurrentNpcAnchor == bram.transform,
                    "Marra's conversation did not resolve the authored Bram line to Bram's live model");
                Require(driver.IsTalking, "Bram did not enter Talking when his Marra-scene line was presented");

                DialogueMushroomHandoffCue cue = dialogue.MushroomHandoff;
                Require(cue.IsConfigured && cue.beforeLineIndex == 10 &&
                        cue.recipientSpeaker == "Marra" && cue.mushroom.Id == "goldfoot" &&
                        cue.mushroom.JournalPreviewPrefab != null,
                    "Marra's pre-identification Goldfoot handoff cue is incomplete");
                Require(DialogueCinematics.Instance.PlayMushroomHandoff(
                        cue.mushroom.JournalPreviewPrefab,
                        cue.recipientSpeaker,
                        cue.PresentationHeight,
                        null),
                    "Goldfoot handoff cinematic would not start");
                Require(DialogueCinematics.Instance.IsPropHandoffActive &&
                        DialogueCinematics.Instance.CurrentHandoffProp != null &&
                        DialogueCinematics.Instance.CurrentNpcAnchor == marra.transform,
                    "Goldfoot handoff did not create its live prop or resolve Marra");
                screen.Close();
                Require(!driver.IsTalking, "Bram did not return to Idle when dialogue closed");
                Require(!DialogueCinematics.Instance.IsPropHandoffActive &&
                        DialogueCinematics.Instance.CurrentHandoffProp == null,
                    "closing dialogue did not clean up the handoff prop");
            }
            finally
            {
                if (screen.IsOpen) screen.Close();
                bram.transform.SetPositionAndRotation(originalPosition, originalRotation);
                if (!originallyActive) bram.SetActive(false);
            }

            return "BRAM + MARRA CINEMATIC — PASS: local 2K Bram texture, 6.1s talking loop, " +
                   "three-character live-model resolution, and cancellable Goldfoot handoff";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BramCharacterVerifier] " + message);
        }
    }
}
#endif
