using UnityEngine;

public sealed class ForestPreviewStation : MonoBehaviour
{
    public Renderer indicator;
    public string purpose;
    public bool Active { get; private set; }
    MaterialPropertyBlock block;
    public void Toggle() => SetActive(!Active);
    public void SetActive(bool value)
    {
        Active = value;
        if (!indicator) return;
        block ??= new MaterialPropertyBlock();
        indicator.GetPropertyBlock(block);
        block.SetColor("_BaseColor", value ? new Color(.2f, .8f, .55f) : new Color(.65f, .5f, .28f));
        indicator.SetPropertyBlock(block);
    }
}
