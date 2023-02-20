using UnityEngine;

public class MultiSprite : MonoBehaviour
{
    [SerializeField] private Sprite[] sprites;

    private SpriteRenderer spriteRenderer;
    private int index = 0;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprites[index];
    }

    void Update()
    {
    }

    public bool NextSprite()
    {
      if (index < sprites.Length - 1) {
        index++;
        spriteRenderer.sprite = sprites[index];
        return true;
      } else {
        return false;
      }
    }

    public void ParentBoundingSet(bool enable) {
      GameObject parent = transform.parent.gameObject;
      SpriteRenderer parentSpriteRenderer = parent.GetComponent<SpriteRenderer>();
      parentSpriteRenderer.enabled = enable;
    }

    public void ParentRandomColor() {
      GameObject parent = transform.parent.gameObject;
      SpriteRenderer parentSpriteRenderer = parent.GetComponent<SpriteRenderer>();
      parentSpriteRenderer.color = new Color(Random.value, Random.value, Random.value);
    }

    public void RandomColor() {
      spriteRenderer.color = new Color(Random.value, Random.value, Random.value);
    }
}