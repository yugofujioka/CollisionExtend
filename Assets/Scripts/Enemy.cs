using UnityEngine;


public class Enemy : MonoBehaviour {
    CollisionPart[] col = null;

    void Start() {
        // コリジョンの呼び出し
        this.col = this.GetComponentsInChildren<CollisionPart>(true);
        for (int i = 0; i < this.col.Length; ++i) {
            this.col[i].Initialize(COL_CATEGORY.ENEMY, HitCallback);
            this.col[i].WakeUp();
        }
    }

    void OnDestroy() {
        // コリジョンの返却
        for (int i = 0; i < this.col.Length; ++i)
            this.col[i].Sleep();
    }

    void Update() {
        float elapsedTime = Time.deltaTime;
        for (int i = 0; i < this.col.Length; ++i)
            this.col[i].Run(elapsedTime);
    }
    
    /// <summary>
    /// 接触コールバック
    /// </summary>
    /// <param name="atk">影響を与えるコリジョン</param>
    /// <param name="def">影響を受けるコリジョン</param>
    private static void HitCallback(Collision atk, Collision def) {
        // atkに自身、defに相手（この場合は敵）が受け渡される
        Debug.LogWarning("ENEMY HIT !!!");

        //atk.Sleep(); // 自身のコリジョンの返却
    }
}