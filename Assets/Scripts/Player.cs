using UnityEngine;


public class Player : MonoBehaviour {
    CollisionPart col = null;

    void Start() {
        // コリジョンの呼び出し
        this.col = this.GetComponentInChildren<CollisionPart>(true);
        this.col.Initialize(COL_CATEGORY.PLAYER, HitCallback);
        this.col.WakeUp();
    }

    void OnDestroy() {
        // コリジョンの返却
        this.col.Sleep();
    }

    void Update() {
        float elapsedTime = Time.deltaTime;
        this.col.Run(elapsedTime);
    }
    
    /// <summary>
    /// 接触コールバック
    /// </summary>
    /// <param name="atk">影響を与えるコリジョン</param>
    /// <param name="def">影響を受けるコリジョン</param>
    private void HitCallback(Collision atk, Collision def) {
        // atkに自身、defに相手（この場合は敵）が受け渡される
        Debug.Log("PLAYER HIT !!!");

        //atk.Sleep(); // 自身のコリジョンの返却
    }
}