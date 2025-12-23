using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[SelectionBase]
[RequireComponent(typeof(HexView))]
public class HexItem : MonoBehaviour
{
    [SerializeField] private HexView view;
    [SerializeField] private HexType type;
    [SerializeField] private HexValueType itemType;
    [SerializeField] private HexValueOverlay overlayType = HexValueOverlay.None;
    [SerializeField] private int groupID = -1;

    private HexCell cell;
    private MovementState movementState = MovementState.idle;
    private int health = 0;
    private int overlayHealth = 1;
    private int sortingGroupAddition = 10;

    public bool IsGroupItem = false;
    public bool CanDestructible = false;
    public bool CanFall = true;
    public bool HasOverlay = false;
    public bool IsGoalItem = false;

    public HexCell Cell => cell;
    public HexType Type => type;
    public HexValueType ValueType => itemType;
    public MovementState MovementState => movementState;
    public int GroupID => groupID;
    private List<GroupItem> groupItems = new List<GroupItem>();


    private void Awake()
    {
        if (view == null) view = GetComponent<HexView>();
    }

    public void Configure(HexType newType, HexValueType newValue, HexValueOverlay newOverlay, int gID = -1)
    {
        type = newType;
        itemType = newValue;
        overlayType = newOverlay;
        groupID = gID;
        health = 0;
        CanFall = true;
        CanDestructible = false;
        IsGoalItem = false;
        HasOverlay = newOverlay != HexValueOverlay.None;
        IsGroupItem = groupID >= 0;
        switch (type)
        {
            case HexType.Box:
            case HexType.Locked:
                CanDestructible = true;
                health = 3;
                break;
            case HexType.Rock:
            case HexType.Pound:
                break;
            case HexType.MailBox:
                CanFall = false;
                break;
            case HexType.Fish:
                IsGoalItem = true;
                break;
            case HexType.Potions:
                CanFall = false;
                health = 5;
                CanDestructible = true;
                break;
        }

        if (IsGroupItem) CanFall = false;
        view.UpdateAll(type, itemType, overlayType, health, CanDestructible);
    }



    public void SetCellAtStart(HexCell startCell)
    {
        view.SetSortingOrder(0);
        cell = startCell;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        startCell.SetFull(this);
    }

    public void FillCell(HexCell cell)
    {
        view.SetSortingOrder(0);
        transform.localPosition = new Vector3(0, 5, 0);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        this.cell = cell;
        cell.SetFull(this);

        view.PlayFallAnimation(Vector3.zero, 0.25f);
    }

    public void SetCell(HexCell newCell)
    {
        cell = newCell;
    }

    public void ClearCell()
    {
        if (cell != null)
        {
            cell.SetEmpty();
            cell = null;
        }

        if (type == HexType.Hex)
        {
            EventManager.Instance.Raise(GameEvent.SelectedItem, this);
        }
        else
        {
            gameObject.SetActive(false);
            EventManager.Instance.Raise(GameEvent.OnMatchType, new MatchTypeHandler(type, transform.position));
        }
        transform.SetParent(null);
    }

    internal void TryRemoveOverlay()
    {
        if (!HasOverlay) return;

        overlayHealth--;
        view.SetOverlayVisual(overlayType, overlayHealth);

        if (overlayHealth <= 0)
        {
            BreakOverlay();
        }
    }

    private void BreakOverlay()
    {
        var tempOverlay = overlayType;
        HasOverlay = false;
        overlayType = HexValueOverlay.None;

        view.SetOverlayVisual(HexValueOverlay.None, 0);
        view.UpdateHealthText(health, CanDestructible);

        EventManager.Instance.Raise(GameEvent.OnMatchOverlay, new MatchOverlayHandler(tempOverlay, transform.position));
    }

    internal void TryDestruct()
    {
        if (!CanDestructible) return;

        health--;
        view.UpdateHealthText(health, true);

        if (health <= 0)
        {
            BreakBase();
        }
    }

    private void OnGroupItemBreak()
    {
        if(!IsGroupItem) return;
        EventManager.Instance.Raise(GameEvent.OnMatchType, new MatchTypeHandler(Type, transform.position));
    }

    private void BreakBase()
    {
        ClearCell();
    }

    internal bool IsBroken()
    {
        return health <= 0;
    }

    public Sequence PlayFallPath(List<FallStep> steps, System.Action onFinish)
    {
        if (steps == null || steps.Count < 2)
        {
            onFinish?.Invoke();
            return null;
        }

        transform.position = steps[0].cell.transform.position;

        Sequence seq = DOTween.Sequence();
        float stepDuration = 0.1f;

        for (int i = 1; i < steps.Count; i++)
        {
            Vector3 targetPos = steps[i].cell.transform.position;
            float startTime = steps[i].tick * stepDuration;

            seq.Insert(
                startTime / 2,
                view.DoMove(targetPos, stepDuration).SetEase(Ease.Linear)
            );
        }

        seq.OnComplete(() =>
        {
            cell = steps[steps.Count - 1].cell;
            onFinish?.Invoke();
        });

        return seq;
    }

    internal void CheckGoalReward()
    {
        if (type == HexType.MailBox)
        {
            GiveMailReward();
        }
        else if (type == HexType.HoneyComb)
        {
            GiveHoneyReward();
        }
    }

    private void GiveHoneyReward()
    {
        EventManager.Instance.Raise(GameEvent.OnMatchType, new MatchTypeHandler(type, transform.position));
    }

    private void GiveMailReward()
    {
        EventManager.Instance.Raise(GameEvent.OnMatchType, new MatchTypeHandler(type, transform.position));
    }

    internal void MoveToMatchSlot(MatchSlot matchSlot)
    {
        view.SetSortingOrder(sortingGroupAddition);
        movementState = MovementState.moving;

        var startPos = transform.position;
        var endPos = matchSlot.transform.position;
        float sign = endPos.x >= 0f ? -1f : 1f;
        float arcSize = 2f;

        var mid = Vector3.Lerp(startPos, endPos, 0.5f);
        mid += new Vector3(arcSize * sign, 0f, 0f);

        Vector3[] path = { startPos, mid, endPos };

        transform.SetParent(matchSlot.transform);

        view.DoPath(path, 0.25f)
            .AppendInterval(0.25f)
            .OnComplete(() =>
            {
                movementState = MovementState.idle;
                EventManager.Instance.Raise(GameEvent.MatchItemArrived, this);
            });
    }

    internal void UpdateMatchSlot(MatchSlot matchSlot)
    {
        view.SetSortingOrder(sortingGroupAddition);
        transform.SetParent(matchSlot.transform);
        movementState = MovementState.moving;

        DOTween.Sequence()
            .Append(view.DoMove(matchSlot.transform.position, 0.25f).SetEase(Ease.Linear))
            .AppendInterval(0.25f)
            .OnComplete(() =>
            {
                movementState = MovementState.idle;
                EventManager.Instance.Raise(GameEvent.MatchItemArrived, this);
            });
    }

    internal void OnMatchComplete()
    {
        EventManager.Instance.Raise(GameEvent.OnMatchValue, new MatchValueHandler(itemType, transform.position));
        EventManager.Instance.Raise(GameEvent.OnMatchType, new MatchTypeHandler(type, transform.position));
    }

    internal bool PredictIsBroken()
    {
        if (health - 1 == 0)
        {
            return true;
        }
        return false;
    }

    internal void SetGroupItem() // DEPRECATED: prefer Configure() groupID logic
    {
        CanFall = false;
        IsGroupItem = true;
        CanDestructible = true;
        switch (type)
        {
            case HexType.Potions:

                break;
            default:
                break;
        }

    }
}
public enum MovementState
{
    idle,
    moving
}

public enum HexValueType
{
    None,
    HeadSet,
    GamePad,
    SmartWatch,
    Radio,
    Printer,
}

public enum HexType
{
    Hex,
    Box,
    Locked,
    Rock,
    Fish,
    Pound,
    MailBox,
    HoneyComb,
    Potions

}

public enum HexValueOverlay
{
    None,
    Ice,
    Web,
    Leaves,
}