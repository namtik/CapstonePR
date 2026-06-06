using UnityEngine;

public enum NodeType { Combat, Shop, Rest, Elite, Boss, Event, Relic }

// ���� ����� ���¿� ������ �����ϴ� ������Ʈ
public class MapNode : MonoBehaviour
{
    public int nodeIndex = -1;
    public MapManager mapManager;
    public bool isCleared = false;
    public RoundData roundData;
    
    [Header("��� Ÿ�� (���� ���� ����)")]
    [SerializeField] private NodeType _nodeType = NodeType.Combat;
    
    public NodeType nodeType
    {
        get
        {
            // roundData�� ������ �װ� ���, ������ _nodeType ���
            if (roundData != null)
                return roundData.roundType;
            return _nodeType;
        }
        set
        {
            _nodeType = value;
        }
    }

    [Header("���־� ����")]
    public NodeVisualConfig visualConfig;

    private UnityEngine.UI.Image img;
    private bool isHighlighted = false;
    private Color highlightColor = Color.white;
    private bool isCurrentPosition = false; // ����: ���� �÷��̾� ��ġ ǥ��

    void Awake()
    {
        img = GetComponent<UnityEngine.UI.Image>();
        
        // Image�� raycastTarget Ȱ��ȭ
        if (img != null && !img.raycastTarget)
        {
            img.raycastTarget = true;
        }
    }

    void Start()
    {
        UpdateVisual();
    }

    // ����: ȭ�� ǥ�� ������Ʈ (�켱����: Ŭ���� > ������ġ > ���̶���Ʈ > Ÿ��)
    void UpdateVisual()
    {
        if (img == null) return;
        
        // ����: Ŭ����� ���� ȸ��
        if (isCleared)
        {
            img.color = visualConfig != null ? visualConfig.clearedColor : Color.gray;
            // ũ��� ���� (Ŭ���� �Ŀ��� ���� ��ġ�� ũ�� ����)
            if (!isCurrentPosition)
            {
                transform.localScale = Vector3.one;
            }
            return;
        }
        
        // ����: ���� ��ġ�� ũ��� Outline���θ� ǥ�� (������ Ÿ�Ժ� ����)
        // isCurrentPosition�� ���� �Ʒ� Ÿ�Ժ� ����/�̹��� ����
        
        // ����: ���� ����
        if (isHighlighted)
        {
            img.color = Color.black;
            if (!isCurrentPosition)
            {
                transform.localScale = Vector3.one;
            }
            return;
        }

        // ����: �⺻ Ÿ�Ժ� ��������Ʈ �Ǵ� ����
        // ũ��� ���� ��ġ�� �ƴϸ� 1.0���� ����
        if (!isCurrentPosition)
        {
            img.color = Color.black;
            transform.localScale = Vector3.one;
        }
        
        if (visualConfig != null)
        {
            Sprite typeSprite = visualConfig.GetSpriteForType(nodeType);
            if (typeSprite != null)
            {
                img.sprite = typeSprite;
                img.color = Color.black;
            }
            else
            {
                img.color = visualConfig.GetColorForType(nodeType);
            }
        }
        else
        {
            // ����: �⺻ ����
            switch (nodeType)
            {
                case NodeType.Combat: img.color = Color.red; break;
                case NodeType.Shop: img.color = Color.green; break;
                case NodeType.Rest: img.color = Color.cyan; break;
                case NodeType.Elite: img.color = Color.yellow; break;
                case NodeType.Boss: img.color = Color.magenta; break;
                case NodeType.Event: img.color = Color.blue; break;
                case NodeType.Relic: img.color = new Color(1f, 0.65f, 0.1f); break;
            }
        }
    }

    public void SetCleared(bool cleared)
    {
        isCleared = cleared;
        UpdateVisual();
        var btn = GetComponent<UnityEngine.UI.Button>();
        if (btn != null) btn.interactable = !cleared;
    }

    public void Highlight(Color color)
    {
        if (img == null) img = GetComponent<UnityEngine.UI.Image>();
        isHighlighted = true;
        highlightColor = color;
        if (img != null) img.color = highlightColor;
        UpdateVisual();
    }

    /// <summary>
    /// ���� �÷��̾� ��ġ�� Ư�� ���̶���Ʈ
    /// ����: ũ�� Ȯ��θ� ǥ�� (���� ���)
    /// </summary>
    public void HighlightAsCurrentPosition(float scale)
    {
        if (img == null) img = GetComponent<UnityEngine.UI.Image>();
        
        isCurrentPosition = true;
        
        // ����: ũ�� Ȯ�븸 ����
        transform.localScale = Vector3.one * scale;
        
        UpdateVisual();
    }

    /// <summary>
    /// ���� ��ġ ���̶���Ʈ ����
    /// </summary>
    public void ClearCurrentPositionHighlight()
    {
        if (!isCurrentPosition) return;
        
        isCurrentPosition = false;
        
        // ũ�⸦ ������� ����
        transform.localScale = Vector3.one;
        
        UpdateVisual();
    }

    public void OnClicked()
    {
        if (mapManager != null)
        {
            mapManager.OnNodeSelected(this);
        }
    }
}