using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TextEdit : UIBehaviour {

    [SerializeField]
    private ScrollRect m_scrollView;
    [SerializeField]
    private InputField m_inputField;

    [SerializeField]
    private bool m_AutoHeight=true;
    [SerializeField]
    private bool m_AutoWidth=true;

    public bool autoHeight
    {
        get
        {
            return m_AutoHeight;
        }
        set
        {
            m_AutoHeight = true;
            CalculationInputPanelSize();
            WaitEndFrameMakeCaretInViewport();
        }
    }

    public bool autoWidth
    {
        get { return m_AutoWidth; }
        set {
            m_AutoWidth = true;
            CalculationInputPanelSize();
            WaitEndFrameMakeCaretInViewport();
        }
    }

    bool initSuccess = false;
    bool onInputFieldSelect;
    private TextGenerator m_TextCacheForLayout;
    public TextGenerator cachedTextGeneratorForLayout
    {
        get { return m_TextCacheForLayout ?? (m_TextCacheForLayout = new TextGenerator()); }
    }
    private bool onWaittingMakeCaretInViewport = false;

    private int preCaretPostion=-1;
    protected override void Awake()
    {
        base.Awake();
        m_inputField.onValueChanged.AddListener(OnInputValueChanged);
    }

    protected override void Start()
    {
        base.Start();
        StartCoroutine(WaitStart());
    }
    IEnumerator WaitStart()
    {
        yield return new WaitForEndOfFrame();
        CalculationInputPanelSize();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        onWaittingMakeCaretInViewport = false;
        CalculationInputPanelSize();
        WaitEndFrameMakeCaretInViewport();
    }

    private void OnInputValueChanged(string value)
    {
        CalculationInputPanelSize();
        WaitEndFrameMakeCaretInViewport();
    }
    /// <summary>
    /// 计算InputField的大小
    /// </summary>
    private void CalculationInputPanelSize()
    {
        string textValue = m_inputField.text + "  ";
        Text inputTextComponent = m_inputField.textComponent;
        TextGenerator textGenerator = cachedTextGeneratorForLayout;
        RectTransform inputRectTrans = m_inputField.transform as RectTransform;
        TextGenerationSettings generationSettings = GetGenerationSettings();
        Vector2 textCompontentSize = inputTextComponent.rectTransform.rect.size;
        Vector2 inputSize = inputRectTrans.rect.size;
        float height;
        float width;
        if (m_AutoHeight)
        {
            height = textGenerator.GetPreferredHeight(textValue, generationSettings) / inputTextComponent.pixelsPerUnit;
            height += inputSize.y - textCompontentSize.y;
            if (height < m_scrollView.viewport.rect.size.y)
            {
                height = m_scrollView.viewport.rect.size.y;
            }
        }
        else 
            height = inputRectTrans.rect.height;
        if (m_AutoWidth)
        {
            width = textGenerator.GetPreferredWidth(textValue, generationSettings) / inputTextComponent.pixelsPerUnit;
            width += inputSize.x - textCompontentSize.x;
            if (width < m_scrollView.viewport.rect.size.x)
            {
                width = m_scrollView.viewport.rect.size.x;
            }
        }
        else
            width = inputRectTrans.rect.width;
        
        inputRectTrans.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        inputRectTrans.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_scrollView.content);
    }

    /// <summary>
    /// 计算光标位置，使光标位于可视区域内
    /// </summary>
    private void MakeCaretInViewport()
    {
        if (!m_inputField.isFocused)
        {
            return;
        }


        if (m_scrollView.horizontalScrollbar.size>=1&&m_scrollView.verticalScrollbar.size>=1)
        {
            //编辑区域未超过视口大小，光标一定在可视区域内
            return;
        }
        //获取光标位置
        int caretIndex = m_inputField.caretPosition;
        preCaretPostion = caretIndex;
        Text textComponent = m_inputField.textComponent;
        TextGenerator textGenerator = textComponent.cachedTextGenerator;
        //光标底部位置
        Vector3 caretPosBottom = Vector3.zero;
        //光标顶部位置
        Vector3 caretPosTop = Vector3.zero;
        if (textGenerator.lineCount == 0)
        {
            //没有内容
            return;
        }
        if (caretIndex < textGenerator.characterCount)
        {
            UICharInfo cusorChar = textGenerator.characters[caretIndex];
            caretPosBottom.x = cusorChar.cursorPos.x;
        }
        else
        {
            Debug.LogError("caretIndex out of textGenerator.characterCount");
        }
        caretPosBottom.x /= textComponent.pixelsPerUnit;
        if (caretPosBottom.x > textComponent.rectTransform.rect.xMax)
        {
            caretPosBottom.x = textComponent.rectTransform.rect.xMax;
        }

        int characterLine = DetermineCharacterLine(caretIndex, textGenerator);

        caretPosBottom.y = textGenerator.lines[characterLine].topY / textComponent.pixelsPerUnit;
        caretPosTop = caretPosBottom;
        float height = textGenerator.lines[characterLine].height / textComponent.pixelsPerUnit;
        caretPosBottom.y -= height;
        caretPosBottom = textComponent.transform.TransformPoint(caretPosBottom);
        caretPosTop = textComponent.transform.TransformPoint(caretPosTop);
        caretPosBottom = RectTransformUtility.WorldToScreenPoint(null, caretPosBottom);
        caretPosTop = RectTransformUtility.WorldToScreenPoint(null, caretPosTop);
        Vector2 caretPosBottomInView;
        Vector2 caretPosTopInView;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_scrollView.viewport, caretPosBottom, null, out caretPosBottomInView))
        {
            return;
        }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_scrollView.viewport, caretPosTop, null, out caretPosTopInView))
        {
            return;
        }
        if (m_scrollView.verticalScrollbar.size < 1)
        {
            float heightDif = m_scrollView.content.rect.height - m_scrollView.viewport.rect.height;
            float lineTop = caretPosTopInView.y;

            float lineBottom = caretPosBottomInView.y;

            if (lineTop > 0)
            {
                //超出上边界需要界面向下移动
                float move = lineTop / heightDif;
                float barValue = Mathf.Clamp01(m_scrollView.verticalNormalizedPosition + move);
                m_scrollView.verticalNormalizedPosition = barValue;
            }
            else if (lineBottom < -m_scrollView.viewport.rect.height)
            {
                float move = -m_scrollView.viewport.rect.height - lineBottom;
                float barValue = Mathf.Clamp01(m_scrollView.verticalNormalizedPosition - move / heightDif);
                m_scrollView.verticalNormalizedPosition = barValue;
            }
        }
        if (m_scrollView.horizontalScrollbar.size < 1)
        {
            float caretP = caretPosTopInView.x;
            float widthDif = m_scrollView.content.rect.width - m_scrollView.viewport.rect.width;
          
            if (caretP < 0)
            {
                //超出做边界
                float move = -caretP / widthDif;
                float barValue = Mathf.Clamp01(m_scrollView.horizontalNormalizedPosition - move);
                m_scrollView.horizontalNormalizedPosition = barValue;
            }
            else if (caretP > m_scrollView.viewport.rect.width)
            {
                float move = caretP - m_scrollView.viewport.rect.width;
                float barValue = Mathf.Clamp01(m_scrollView.horizontalNormalizedPosition + move);
                m_scrollView.horizontalNormalizedPosition = barValue;
            }
        }
    }

    /// <summary>
    /// 等待到帧结束时计算光标位置
    /// </summary>
    private void WaitEndFrameMakeCaretInViewport()
    {
        if (!onWaittingMakeCaretInViewport)
        {
            onWaittingMakeCaretInViewport = true;
            StartCoroutine(IEWaitEndFrameMakeCaretInViewport());
        }
    }

    private IEnumerator IEWaitEndFrameMakeCaretInViewport()
    {
        yield return new WaitForEndOfFrame();
        MakeCaretInViewport();
        onWaittingMakeCaretInViewport = false;
    }
    /// <summary>
    /// 确定字符在第多少行
    /// </summary>
    /// <param name="charPos">字符的位置</param>
    /// <param name="generator">文本生成器</param>
    /// <returns>返回行下标</returns>
    private int DetermineCharacterLine(int charPos, TextGenerator generator)
    {
        for (int i = 0; i < generator.lineCount - 1; ++i)
        {
            if (generator.lines[i + 1].startCharIdx > charPos)
                return i;
        }

        return generator.lineCount - 1;
    }

    private TextGenerationSettings GetGenerationSettings()
    {
       return m_inputField.textComponent.GetGenerationSettings(m_inputField.textComponent.rectTransform.rect.size);
    }

    void LateUpdate()
    {
        if (preCaretPostion != m_inputField.caretPosition && !onWaittingMakeCaretInViewport)
        {
            MakeCaretInViewport();
        }
    }


}
