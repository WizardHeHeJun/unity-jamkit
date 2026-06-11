using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DialogueGraph
{
    /// <summary>
    /// 对话框 UI：名字板 + 打字机正文 + 选项面板。
    /// 点击推进由覆盖全屏的透明 Button 调 NotifyAdvanceClicked() 触发。
    /// </summary>
    public class VNDialogueUI : MonoBehaviour
    {
        [Header("对话框")]
        public GameObject dialoguePanel;
        public GameObject namePlate;
        public TMP_Text nameText;
        public TMP_Text bodyText;

        [Header("选项")]
        public GameObject choicePanel;
        public Button choiceButtonTemplate;

        [Header("打字机（字/秒，0 = 直接全显）")]
        public float charsPerSecond = 30f;

        public bool IsTyping { get; private set; }

        public event Action OnAdvanceClicked;
        public event Action<int> OnChoiceSelected;

        Coroutine typingRoutine;
        readonly List<Button> spawnedButtons = new List<Button>();

        /// <summary>由全屏透明点击层的 Button.onClick 调用。</summary>
        public void NotifyAdvanceClicked()
        {
            OnAdvanceClicked?.Invoke();
        }

        public void ShowLine(string speakerName, Color nameColor, string text, bool instant = false)
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(true);

            bool hasName = !string.IsNullOrEmpty(speakerName);
            if (namePlate != null) namePlate.SetActive(hasName);
            if (nameText != null)
            {
                nameText.text = speakerName;
                nameText.color = nameColor;
            }

            if (typingRoutine != null) StopCoroutine(typingRoutine);
            bodyText.text = text ?? string.Empty;

            if (instant || charsPerSecond <= 0f)
            {
                bodyText.maxVisibleCharacters = 99999;
                IsTyping = false;
                typingRoutine = null;
            }
            else
            {
                typingRoutine = StartCoroutine(TypeText());
            }
        }

        IEnumerator TypeText()
        {
            IsTyping = true;
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;

            float progress = 0f;
            while (bodyText.maxVisibleCharacters < total)
            {
                progress += Time.deltaTime * charsPerSecond;
                bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(progress));
                yield return null;
            }

            IsTyping = false;
            typingRoutine = null;
        }

        public void CompleteTyping()
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;
            bodyText.maxVisibleCharacters = 99999;
            IsTyping = false;
        }

        public void ShowChoices(IReadOnlyList<string> options)
        {
            HideChoices();
            if (choicePanel == null || choiceButtonTemplate == null) return;

            choicePanel.SetActive(true);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var button = Instantiate(choiceButtonTemplate, choicePanel.transform);
                button.gameObject.SetActive(true);

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = options[i];

                button.onClick.AddListener(() =>
                {
                    HideChoices();
                    OnChoiceSelected?.Invoke(index);
                });
                spawnedButtons.Add(button);
            }
        }

        public void HideChoices()
        {
            foreach (var button in spawnedButtons)
            {
                if (button != null) Destroy(button.gameObject);
            }
            spawnedButtons.Clear();
            if (choicePanel != null) choicePanel.SetActive(false);
        }

        public void HideAll()
        {
            CompleteTyping();
            HideChoices();
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
        }
    }
}
