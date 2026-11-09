using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>씬에 놓인 고정 글. 문장 대신 텍스트 표의 키만 든다.</summary>
    [RequireComponent(typeof(UnityEngine.UI.Text))]
    public sealed class TextLabel : MonoBehaviour
    {
        [SerializeField] private string textKey;

        public string TextKey
        {
            get => textKey;
            set => textKey = value;
        }

        private void OnEnable()
        {
            GetComponent<UnityEngine.UI.Text>().text = Text.Get(textKey);
        }
    }
}
