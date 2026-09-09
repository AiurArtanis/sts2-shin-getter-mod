# mod-v1.2.1 Release Draft

Status: awaiting Artanis review; not merged, tagged, packaged, or published.

- Target: stable game 0.109; manifest minimum remains 0.107.0.
- Tag: `mod-v1.2.1`
- Asset: `shin-getter-mod-v1.2.1.zip`
- Candidate release page: https://github.com/AiurArtanis/sts2-shin-getter-mod/releases/tag/mod-v1.2.1
- Base: `bcd321a79ebcde24d85ed11a96c03eaa710931bb`

## Publication Gates (Internal)

- Obtain Artanis approval before merging this PR or publishing.
- The #192 shop-record serialization change is already in main (`a1ba89af`, PR #211). The additional SavedPropertiesTypeCache registration fix (`daba4987e699a50981187f1fac93a6a18f73e537`) is NOT part of this draft's base; do not claim the additional fix is delivered without integrating it.
- The latest four-player five-scenario regression was stopped after 1/5 scenarios. Do not claim a complete four-player regression pass.
- A new-run Godot C# binding fatal was reported against the older daily deployment on 2026-09-09. The trigger and product responsibility are unresolved. Preserve that environment and resolve the release decision before publishing; this documentation task does not deploy or restart tests.
- Keep 0.111 Beta on its existing version and download. #197 is not a new stable-109 fix. Unimplemented BGM, dialogue, card-art and balance requests are excluded.
- At publication, replace the preparation labels and update stable download/install links to the actual published asset. Leave historical v1.2.0 and Beta links intact.

## Merged Sources

| Issue | Main commit(s) | Scope |
| --- | --- | --- |
| #187 | `90662124` | Ordinary transformation versus opening fusion |
| #191 | `d5d4ef96`, `d4c6ffbb` | Awaited Chain Reaction and local voice-history isolation |
| #192 | `a1ba89af` | Good Citizen Card shop-record serialization only |
| #195 | `9c0afdc6` | Event-specific character visuals |
| #196 | `038c3946` | Hedgehog Tactic condition highlighting |
| #198 | `3fca24d6` | Holy Dragon Roar description |
| #216 | `bcd321a7` | Nullable Power model access in icon refresh |

## zhs

### Release Notes

- 修复三合一木雕连续变形三次后，一号机模型消失；普通变形不再被误认为开场融合。 (#187)
- 修复连锁反应的多人结算时序；活力减少后的再生、覆甲按顺序完成。纯本地台词播放记录不再参与联机状态校验。 (#191)
- 修复好市民证商店记录的存档序列化异常，避免相关事件后保存失败。 (#192)
- 修正假商人与建筑师事件中的角色画面：分别显示流龙马静态图与一号机待机。 (#195)
- 补齐刺猬战术在三号机／真盖塔龙条件下的黄色高亮，三语一致，实际效果不变。 (#196)
- 修正圣龙咆哮三语说明为消耗所有盖塔卡，不再错误限定为手牌；数值和结算范围未改。 (#198)
- 修复状态图标在模型尚未就绪时的访问异常，保留闪光和颜色过渡效果。 (#216)

0.111 Beta 仍使用 v1.2.0-beta.111 与 shin-getter-mod-v1.2.0(111-beta).zip，下载位置仍是 mod-v1.2.0；本次未发布新的 Beta 包。正式与 Beta 的四件套不可混用。

### changeNote

v1.2.1（正式109）：修复木雕变形后模型消失、连锁反应联机结算与本地台词同步边界、好市民证商店记录保存、事件角色显示及状态图标异常；修正刺猬战术高亮与圣龙咆哮三语说明。无新增卡牌或平衡调整。111 Beta 保持独立 v1.2.0-beta.111。

## eng

### Release Notes

- Fixed Shin Getter 1 disappearing after Triple Wood Carving transforms three times. Ordinary transformations no longer trigger opening-fusion preparation. (#187)
- Fixed multiplayer resolution timing for Chain Reaction so Regen and Plating finish in order after Vigor is lost. Local voice-play history no longer participates in multiplayer state checks. (#191)
- Fixed save serialization of Good Citizen Card shop records, preventing related post-event save failures. (#192)
- Corrected character visuals in the Fake Merchant and Architect events: a static Ryoma image and Shin Getter 1 idle, respectively. (#195)
- Restored Hedgehog Tactic's yellow condition highlighting for Shin Getter 3 / Shin Getter Dragon in all three languages. Gameplay effects are unchanged. (#196)
- Corrected Holy Dragon Roar in all three languages to say it exhausts all Getter cards, not only cards in hand. Values and resolution scope are unchanged. (#198)
- Guarded status-icon access before its model is ready, preserving flashes and color transitions. (#216)

0.111 Beta remains v1.2.0-beta.111, using shin-getter-mod-v1.2.0(111-beta).zip from mod-v1.2.0. This update does not publish a new Beta package. Do not mix the stable and Beta four-file packages.

### changeNote

v1.2.1 (stable 109): fixes transformation visibility, Chain Reaction multiplayer timing and local voice-history isolation, Good Citizen Card shop-record saves, event visuals, and status-icon errors. Corrects Hedgehog Tactic highlighting and Holy Dragon Roar text in all three languages. No new cards or balance changes. 111 Beta remains the separate v1.2.0-beta.111 build.

## jpn

### Release Notes

- 三位一体の木彫りで3回変形すると真ゲッター1が消える不具合を修正。通常の変形を戦闘開始時の合体準備として扱わないようにしました。 (#187)
- 連鎖反応のマルチプレイでの処理順を修正し、活力減少後の再生・プレートの付与を順番に完了させます。ローカルのボイス再生履歴を同期状態の照合から除外しました。 (#191)
- 良き市民証のショップ記録の保存形式を修正し、関連イベント後の保存エラーを防ぎます。 (#192)
- 偽商人と建築家イベントのキャラクター表示を、それぞれ流竜馬の静止画と真ゲッター1の待機状態に修正しました。 (#195)
- ハリネズミ戦術の真ゲッター3／真ゲッタードラゴン条件の黄色強調を3言語で修正。実際の効果は変更していません。 (#196)
- 聖龍咆哮の3言語の説明を、手札だけではなくすべてのゲッターカードを廃棄する表記に修正。数値と処理範囲は変更していません。 (#198)
- モデルの準備前に状態アイコンへアクセスする際のエラーを修正。点滅と色の遷移は維持しています。 (#216)

0.111 Betaは引き続きv1.2.0-beta.111です。mod-v1.2.0内のshin-getter-mod-v1.2.0(111-beta).zipをご利用ください。今回は新しいBetaパッケージを公開しません。正式版とBeta版の4ファイルを混在させないでください。

### changeNote

v1.2.1（正式版109）：変形後のモデル消失、連鎖反応のマルチプレイ処理とローカルボイス履歴の同期除外、良き市民証のショップ記録保存、イベント表示、状態アイコンの不具合を修正。ハリネズミ戦術の強調表示と聖龍咆哮の3言語説明を修正しました。新カード・バランス変更はありません。111 Betaは別パッケージのv1.2.0-beta.111を維持します。
