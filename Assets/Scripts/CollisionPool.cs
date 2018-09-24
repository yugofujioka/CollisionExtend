using UnityEngine;
using TaskSystem;


/// <summary>
/// コリジョン管理
/// 真円と矩形（回転あり）のみ対応
/// </summary>
public sealed class CollisionPool {
    #region DEFINE
    public const int POOL_MAX = 100;   // 全体のコリジョン数
    public const int POOL_PL_MAX = 10; // 味方用のコリジョン数
    public const int POOL_EN_MAX = 10; // 敵用のコリジョン数
    #endregion


    #region MEMBER
    private Collision[] collisions = new Collision[POOL_MAX];
    private TaskSystem<Collision> pool = new TaskSystem<Collision>(POOL_MAX);       // 大元のプール
    private TaskSystem<Collision> players = new TaskSystem<Collision>(POOL_PL_MAX); // 味方
    private TaskSystem<Collision> enemies = new TaskSystem<Collision>(POOL_EN_MAX); // 敵

    private OrderHandler<Collision> playerOrderHandler = null; // 味方コリジョンの処理
    private MatchHandler<Collision> scanOrderHandler = null;   // Hitしたかのチェック処理
    private OrderHandler<Collision> hitOrderHandler = null;    // Hitした際の処理

    private Collision ccol = null;  // チェックするコリジョン
    #endregion


    #region MAIN FUNCTION
    /// <summary>
    /// 初期化
    /// </summary>
    public void Initialize() {
        // 味方検査
        this.playerOrderHandler = new OrderHandler<Collision>(this.PlayerOrder);
        // 接触判定
        this.scanOrderHandler = new MatchHandler<Collision>(this.ScanOrder);
        // 接触処理
        this.hitOrderHandler = new OrderHandler<Collision>(this.HitOrder);

        for (int i = 0; i < POOL_MAX; ++i) {
            this.collisions[i] = new Collision();
            this.pool.Attach(this.collisions[i]);
        }

#if DEBUG
        this.InitializeDebug();
#endif
    }

    /// <summary>
    /// 終了処理
    /// </summary>
    public void Final() {
        this.Clear();
#if DEBUG
        this.FinalizeDebug();
#endif
    }

    /// <summary>
    /// 更新
    /// </summary>
    /// <param name="elapsedTime">経過時間</param>
    public void Proc(float elapsedTime) {
        this.players.Order(this.playerOrderHandler); // 接触判定

#if DEBUG
        if (this.debugCamera != null) {
            this.enemies.Action(this.displayHandler);
            this.players.Action(this.displayHandler);
        }
#endif
    }
    #endregion


    #region PUBLIC FUNCTION
    /// <summary>
    /// コリジョンの取得
    /// </summary>
    /// <param name="category">コリジョン種別</param>
    /// <param name="hitHandler">接触コールバック</param>
    public Collision PickOut(COL_CATEGORY category, HitHandler hitHandler = null) {
        if (this.pool.count < 1) {
            Debug.Assert(false, "コリジョン不足");
            return null;
        }

        Collision col = this.pool.PickOutLast();
        col.WakeUp(category, hitHandler);
        switch (category) {
            case COL_CATEGORY.PLAYER:
                this.players.Attach(col);
                break;
            case COL_CATEGORY.ENEMY:
                this.enemies.Attach(col);
                break;
        }

        return col;
    }

    /// <summary>
    /// 強制全回収
    /// </summary>
    public void Clear() {
        this.pool.Clear();
        this.players.Clear();
        this.enemies.Clear();
        for (int i = 0; i < POOL_MAX; ++i) {
            this.collisions[i].Sleep();
            this.pool.Attach(this.collisions[i]);
        }
    }
    #endregion


    #region PRIVATE FUNCTION
    /// <summary>
    /// 味方関連命令
    /// </summary>
    /// <param name="pcol">味方コリジョン</param>
    /// <param name="no">命令No.</param>
    /// <returns>引き続き有効か</returns>
    private bool PlayerOrder(Collision pcol, int no) {
        this.ccol = pcol;
        // 敵とのチェック
        this.enemies.ParticularOrder(this.scanOrderHandler, this.hitOrderHandler);

        if (!this.ccol.enable)
            this.pool.Attach(this.ccol);

        return this.ccol.enable;
    }

    /// <summary>
    /// 接触判定命令
    /// </summary>
    /// <param name="tcol">判定コリジョン</param>
    /// <returns>-1:中断, 0:未接触, 1:接触</returns>
    private int ScanOrder(Collision tcol) {
        if (!tcol.enable)
            return 0;

        if (!ccol.enable)
            return -1;

        bool hit = false;
        switch (ccol.form) {
            // 自分:円
            case COL_FORM.CIRCLE:
                // 対象:円
                if (tcol.form == COL_FORM.CIRCLE) {
                    hit = Vector2.Distance(ccol.point, tcol.point) <= (ccol.range + tcol.range);
                    break;
                }

                // 対象:矩形
                hit = CollisionPool.IsOverlapCircleToRect(ccol, tcol);
                break;
            // 自分:矩形
            case COL_FORM.RECTANGLE:
                // 対象:円
                if (tcol.form == COL_FORM.CIRCLE) {
                    hit = CollisionPool.IsOverlapCircleToRect(tcol, ccol);
                    break;
                }

                // 対象:矩形
                hit = CollisionPool.HitRectangles(ccol, tcol);
                break;
        }

#if DEBUG
        ccol.hit |= hit;
        tcol.hit |= hit;
#endif

        return (hit ? 1 : 0);
    }

    /// <summary>
    /// 接触処理命令
    /// </summary>
    /// <param name="tcol">接触したコリジョン</param>
    /// <param name="no">命令No.</param>
    /// <returns>まだ有効か</returns>
    private bool HitOrder(Collision tcol, int no) {
        if (this.ccol.hitHandler != null)
            this.ccol.hitHandler(this.ccol, tcol);
        if (tcol.hitHandler != null)
            tcol.hitHandler(tcol, this.ccol);

        return true;
    }
	
	static System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
    /// <summary>
    /// 円と矩形の接触判定
    /// </summary>
    /// <param name="ccol">円コリジョン</param>
    /// <param name="rcol">矩形コリジョン</param>
    private static bool IsOverlapCircleToRect(Collision ccol, Collision rcol) {
        // 最大距離チェック
        if (Vector2.Distance(ccol.point, rcol.point) > (ccol.range + rcol.range))
            return false;

        float rad = -rcol.angle * Mathf.Deg2Rad;
        float rCos = (float)System.Math.Cos(rad);
        float rSin = (float)System.Math.Sin(rad);

        // MEMO: 円はオフセットを反映しない（接続ノードをあらかじめ用意する）
        // ①矩形からの相対距離をとる
        Vector2 pos = ccol.point - rcol.point;
		// ②円の位置を回転前に戻す
		float x = pos.x * rCos - pos.y * rSin;
		float y = pos.x * rSin + pos.y * rCos;
		pos.x = x;
		pos.y = y;
		// ③矩形の中で最も円に近い点を算出
		float halfWidth = rcol.size.x * 0.5f;
        float halfHeight = rcol.size.y * 0.5f;
        Vector2 checkPoint = new Vector2(Mathf.Clamp(pos.x, -halfWidth, halfWidth), Mathf.Clamp(pos.y, -halfHeight, halfHeight));
        // ④接触判定
        return (Vector2.Distance(pos, checkPoint) <= ccol.range);
    }

    static readonly Vector2[] cPt = new Vector2[4];
    static readonly Vector2[] tPt = new Vector2[4];
    /// <summary>
    /// 矩形同士の接触判定
    /// </summary>
    /// <param name="ccol">自分</param>
    /// <param name="tcol">対象</param>
    private static bool HitRectangles(Collision ccol, Collision tcol) {
        Vector2 cpos = ccol.point;
        Vector2 tpos = tcol.point;

        // 絶対に接触しない距離なら判定回避
        float dist = Vector2.Distance(cpos, tpos);
        if (dist > (ccol.range + tcol.range))
            return false;

        float cRad = ccol.angle * Mathf.Deg2Rad;
        float cCos = (float)System.Math.Cos(cRad);
        float cSin = (float)System.Math.Sin(cRad);
        float tRad = tcol.angle * Mathf.Deg2Rad;
        float tCos = (float)System.Math.Cos(tRad);
        float tSin = (float)System.Math.Sin(tRad);
        float cRangeX = ccol.size.x * 0.5f;
        float cRangeY = ccol.size.y * 0.5f;
        float tRangeX = tcol.size.x * 0.5f;
        float tRangeY = tcol.size.y * 0.5f;

        // 自分の4頂点を取得
        cPt[0].x = -cRangeX * cCos -  cRangeY * cSin + cpos.x;
        cPt[0].y = -cRangeX * cSin +  cRangeY * cCos + cpos.y;
        cPt[1].x =  cRangeX * cCos -  cRangeY * cSin + cpos.x;
        cPt[1].y =  cRangeX * cSin +  cRangeY * cCos + cpos.y;
        cPt[2].x =  cRangeX * cCos - -cRangeY * cSin + cpos.x;
        cPt[2].y =  cRangeX * cSin + -cRangeY * cCos + cpos.y;
        cPt[3].x = -cRangeX * cCos - -cRangeY * cSin + cpos.x;
        cPt[3].y = -cRangeX * cSin + -cRangeY * cCos + cpos.y;
        // 対象の4頂点を取得
        tPt[0].x = -tRangeX * tCos -  tRangeY * tSin + tpos.x;
        tPt[0].y = -tRangeX * tSin +  tRangeY * tCos + tpos.y;
        tPt[1].x =  tRangeX * tCos -  tRangeY * tSin + tpos.x;
        tPt[1].y =  tRangeX * tSin +  tRangeY * tCos + tpos.y;
        tPt[2].x =  tRangeX * tCos - -tRangeY * tSin + tpos.x;
        tPt[2].y =  tRangeX * tSin + -tRangeY * tCos + tpos.y;
        tPt[3].x = -tRangeX * tCos - -tRangeY * tSin + tpos.x;
        tPt[3].y = -tRangeX * tSin + -tRangeY * tCos + tpos.y;

        // 線分交差処理はもっと上手く出来るハズ
        // 上辺
        if (CrossLine(ref cPt[0], ref cPt[1], ref tPt[0], ref tPt[1]))
            return true;
        if (CrossLine(ref cPt[0], ref cPt[1], ref tPt[1], ref tPt[2]))
            return true;
        if (CrossLine(ref cPt[0], ref cPt[1], ref tPt[2], ref tPt[3]))
            return true;
        if (CrossLine(ref cPt[0], ref cPt[1], ref tPt[3], ref tPt[0]))
            return true;
        // 右辺
        if (CrossLine(ref cPt[1], ref cPt[2], ref tPt[0], ref tPt[1]))
            return true;
        if (CrossLine(ref cPt[1], ref cPt[2], ref tPt[1], ref tPt[2]))
            return true;
        if (CrossLine(ref cPt[1], ref cPt[2], ref tPt[2], ref tPt[3]))
            return true;
        if (CrossLine(ref cPt[1], ref cPt[2], ref tPt[3], ref tPt[0]))
            return true;
        // 下辺
        if (CrossLine(ref cPt[2], ref cPt[3], ref tPt[0], ref tPt[1]))
            return true;
        if (CrossLine(ref cPt[2], ref cPt[3], ref tPt[1], ref tPt[2]))
            return true;
        if (CrossLine(ref cPt[2], ref cPt[3], ref tPt[2], ref tPt[3]))
            return true;
        if (CrossLine(ref cPt[2], ref cPt[3], ref tPt[3], ref tPt[0]))
            return true;
        // 左辺
        if (CrossLine(ref cPt[3], ref cPt[0], ref tPt[0], ref tPt[1]))
            return true;
        if (CrossLine(ref cPt[3], ref cPt[0], ref tPt[1], ref tPt[2]))
            return true;
        if (CrossLine(ref cPt[3], ref cPt[0], ref tPt[2], ref tPt[3]))
            return true;
        if (CrossLine(ref cPt[3], ref cPt[0], ref tPt[3], ref tPt[0]))
            return true;

        // 対象が自分に内包されているか
        cPt[0] = new Vector2(-cRangeX,  cRangeY);
        cPt[1] = new Vector2( cRangeX,  cRangeY);
        cPt[2] = new Vector2( cRangeX, -cRangeY);
        cPt[3] = new Vector2(-cRangeX, -cRangeY);
        Vector2 point = tpos - cpos;
        Vector2 checkPt;
        checkPt.x = point.x * cCos - point.y * -cSin;
        checkPt.y = point.x * -cSin + point.y * cCos;
        if (CollisionPool.PointInRect(ref checkPt, cPt))
            return true;
        // 自分が対象に内包されているか
        tPt[0] = new Vector2(-tRangeX,  tRangeY);
        tPt[1] = new Vector2( tRangeX,  tRangeY);
        tPt[2] = new Vector2( tRangeX, -tRangeY);
        tPt[3] = new Vector2(-tRangeX, -tRangeY);
        point = cpos - tpos;
        checkPt.x = point.x * tCos - point.y * -tSin;
        checkPt.y = point.x * -tSin + point.y * tCos;
        if (CollisionPool.PointInRect(ref checkPt, tPt))
            return true;

        return false;
    }

    /// <summary>
    /// 任意の点が矩形内に内包されているか
    /// </summary>
    /// <param name="point">任意の点</param>
    /// <param name="center">矩形の中心点</param>
    /// <returns>内包されている</returns>
    private static bool PointInRect(ref Vector2 point, Vector2[] rect) {
        if (point.x < rect[0].x || point.x > rect[1].x)
            return false;
        if (point.y < rect[3].y || point.y > rect[0].y)
            return false;

        return true;
    }

    /// <summary>
    /// 線分の交差チェック
    /// </summary>
    /// <param name="p1">線分１の始点</param>
    /// <param name="p2">線分１の終点</param>
    /// <param name="p3">線分２の始点</param>
    /// <param name="p4">線分２の終点</param>
    /// <returns>交差している</returns>
    private static bool CrossLine(ref Vector2 p1, ref Vector2 p2, ref Vector2 p3, ref Vector2 p4) {
        // x座標によるチェック
        if (p1.x >= p2.x) {
            if ((p1.x < p3.x && p1.x < p4.x) || (p2.x > p3.x && p2.x > p4.x))
                return false;
        } else {
            if ((p2.x < p3.x && p2.x < p4.x) || (p1.x > p3.x && p1.x > p4.x))
                return false;
        }
        // y座標によるチェック
        if (p1.y >= p2.y) {
            if ((p1.y < p3.y && p1.y < p4.y) || (p2.y > p3.y && p2.y > p4.y)) {
                return false;
            }
        } else {
            if ((p2.y < p3.y && p2.y < p4.y) || (p1.y > p3.y && p1.y > p4.y)) {
                return false;
            }
        }

        if (((p1.x - p2.x) * (p3.y - p1.y) + (p1.y - p2.y) * (p1.x - p3.x)) *
            ((p1.x - p2.x) * (p4.y - p1.y) + (p1.y - p2.y) * (p1.x - p4.x)) > 0) {
            return false;
        }
        if (((p3.x - p4.x) * (p1.y - p3.y) + (p3.y - p4.y) * (p3.x - p1.x)) *
            ((p3.x - p4.x) * (p2.y - p3.y) + (p3.y - p4.y) * (p3.x - p2.x)) > 0) {
            return false;
        }

        return true;
    }
    #endregion


#if DEBUG
    public Camera debugCamera = null; // コリジョン表示用カメラ

    // DisplayActionは非staticなのでデリゲートキャッシュする
    private System.Action<Collision> displayHandler = null;
    
    private Mesh debugCircle = null;               // 円形メッシュ
    private Mesh debugQuad = null;                 // 矩形メッシュ
    private Material debugMat = null;              // デバッグ表示用マテリアル
    private MaterialPropertyBlock debugMpb = null; // 色変え用

    /// <summary>
    /// デバッグ用の初期化
    /// </summary>
    private void InitializeDebug() {
        // 円メッシュ作成
        int sampleCount = 32;
        Vector3[] verts = new Vector3[sampleCount * 2 + 2];   // 頂点
        int[] indices = new int[sampleCount * 2 * 3];         // 1辺に対して三角形2つ
        float rad = Mathf.PI * 2f / sampleCount;
        for (int i = 0; i < sampleCount + 1; ++i) {
            float sampleRad = rad * i;
            Vector3 vec = new Vector3(Mathf.Cos(sampleRad), Mathf.Sin(sampleRad), 0f);
            verts[i * 2 + 0] = vec;
            verts[i * 2 + 1] = Vector3.zero;
        }
        verts[sampleCount * 2] = verts[0];
        verts[sampleCount * 2 + 1] = verts[1];
        int index = 0;
        for (int i = 0; i < sampleCount; ++i) {
            indices[i * 6 + 0] = 0 + index;
            indices[i * 6 + 1] = 1 + index;
            indices[i * 6 + 2] = 2 + index;
            indices[i * 6 + 3] = 2 + index;
            indices[i * 6 + 4] = 1 + index;
            indices[i * 6 + 5] = 3 + index;
            index += 2;
        }
        this.debugCircle = new Mesh();
        this.debugCircle.name = "Circle";
        this.debugCircle.vertices = verts;
        this.debugCircle.SetIndices(indices, MeshTopology.Triangles, 0);

        // 矩形メッシュ作成
        verts = new Vector3[4];
        indices = new int[6];
        verts[0] = new Vector3(-0.5f,  0.5f);
        verts[1] = new Vector3(-0.5f, -0.5f);
        verts[2] = new Vector3( 0.5f,  0.5f);
        verts[3] = new Vector3( 0.5f, -0.5f);
        indices[0] = 0;
        indices[1] = 1;
        indices[2] = 2;
        indices[3] = 2;
        indices[4] = 1;
        indices[5] = 3;
        this.debugQuad = new Mesh();
        this.debugQuad.name = "Circle";
        this.debugQuad.vertices = verts;
        this.debugQuad.SetIndices(indices, MeshTopology.Triangles, 0);

        // 適当なマテリアル用意
        this.debugMat = new Material(Shader.Find("GUI/Text Shader"));
        this.debugMat.mainTexture = Texture2D.whiteTexture;
        this.debugMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay; // 最前面
        this.debugMpb = new MaterialPropertyBlock();

        this.displayHandler = new System.Action<Collision>(this.DisplayAction);
    }

    /// <summary>
    /// デバッグメンバーの解放
    /// </summary>
    private void FinalizeDebug() {
        // 動的に作ったMaterialは破棄しないとリーク
        Object.Destroy(this.debugMat);
    }
    
    /// <summary>
    /// 当たり判定表示
    /// </summary>
    /// <param name="col">表示するコリジョン</param>
    private void DisplayAction(Collision col) {
        if (col.hit)
            this.debugMpb.SetColor("_Color", new Color(1f, 1f, 0f, 0.8f)); // 接触カラー黄
        else
            this.debugMpb.SetColor("_Color", new Color(0f, 0f, 1f, 0.8f)); // 通常カラー青
        
        Camera cam = this.debugCamera;
        // 等倍距離に置く
        Vector3 worldPosition = col.point;
        worldPosition.z = ((float)Screen.height * 0.5f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        worldPosition = cam.ScreenToWorldPoint(worldPosition);

        if (col.form == COL_FORM.CIRCLE) {
            // 円形
			Quaternion rot = Quaternion.identity;
			Vector3 scale = Vector3.one * col.range;
			Matrix4x4 matrix = Matrix4x4.TRS(worldPosition, rot, scale);
            Graphics.DrawMesh(this.debugCircle, matrix, this.debugMat,
                              LayerMask.NameToLayer("Default"), cam, 0, this.debugMpb);
        } else {
            // 矩形
            Vector3 scale = new Vector3(col.size.x, col.size.y, 1f);
            Quaternion rot = Quaternion.Euler(0f, 0f, col.angle);
            Matrix4x4 matrix = Matrix4x4.TRS(worldPosition, rot, scale);
            Graphics.DrawMesh(this.debugQuad, matrix, this.debugMat,
                              LayerMask.NameToLayer("Default"), cam, 0, this.debugMpb);
        }

        col.hit = false;
    }
#endif
}
