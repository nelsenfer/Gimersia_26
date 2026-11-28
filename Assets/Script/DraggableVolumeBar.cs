
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DraggableVolumeBar :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    [Header("Bar Settings")]
    [SerializeField] private Image fillImage;

    [Header("Volume Settings")]
    [SerializeField] private string volumeKey = "SoundVolume";
    [SerializeField, Range(0f, 1f)]
    private float defaultVolume = 1f;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource[] audioSources;

    private RectTransform barRect;
    private float currentVolume;

    private void Awake()
    {
        barRect = GetComponent<RectTransform>();

        if (fillImage == null)
        {
            Debug.LogError(
                "[DraggableVolumeBar] Fill Image belum dihubungkan!",
                this
            );

            enabled = false;
            return;
        }

        // Configure fill image
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

        // Biar input diterima oleh bar induk
        fillImage.raycastTarget = false;

        // Load saved volume
        currentVolume = PlayerPrefs.GetFloat(
            volumeKey,
            defaultVolume
        );

        SetVolume(currentVolume);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateVolumeFromMouse(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateVolumeFromMouse(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        PlayerPrefs.SetFloat(volumeKey, currentVolume);
        PlayerPrefs.Save();

        Debug.Log(
            "[" + volumeKey + "] Volume: " +
            Mathf.RoundToInt(currentVolume * 100f) + "%"
        );
    }

    private void UpdateVolumeFromMouse(
        PointerEventData eventData)
    {
        Vector2 localPoint;

        bool insideRect =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                barRect,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint
            );

        if (!insideRect || barRect.rect.width <= 0f)
            return;

        float normalizedValue = Mathf.InverseLerp(
            barRect.rect.xMin,
            barRect.rect.xMax,
            localPoint.x
        );

        SetVolume(normalizedValue);
    }

    private void SetVolume(float value)
    {
        currentVolume = Mathf.Clamp01(value);

        // Update visual bar
        fillImage.fillAmount = currentVolume;

        // Update all assigned audio sources
        foreach (AudioSource source in audioSources)
        {
            if (source != null)
            {
                source.volume = currentVolume;
            }
        }
    }
}
