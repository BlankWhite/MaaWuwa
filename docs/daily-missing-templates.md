# 自动日常首版缺失/建议补充模板

当前 `DailyTick` 为了先跑通流程，主要使用 OCR 与 1280x720 相对坐标。下面这些模板补齐后可以把固定坐标逐步替换成 Maa `TemplateMatch`，稳定性会明显提升：

| 建议文件名 | 用途 |
| --- | --- |
| `daily_guidebook_quest.png` | 索拉指南-日常活跃页签/页面确认 |
| `daily_reward_claim.png` | 日常活跃奖励可领取按钮 |
| `daily_reward_claim_all.png` | 日常奖励领取确认/一键领取 |
| `guidebook_simulation.png` | 索拉指南-模拟领域左侧入口 |
| `simulation_shell_credit.png` | 模拟领域-贝币条目 |
| `simulation_resonator_exp.png` | 模拟领域-共鸣者经验条目 |
| `simulation_weapon_exp.png` | 模拟领域-武器经验条目 |
| `fast_travel_or_go.png` | 前往/传送按钮 |
| `team_start_challenge.png` | 队伍页开始挑战按钮 |
| `f_interact.png` | 副本内 F 交互提示 |
| `claim_stamina_sign.png` | 体力奖励领取弹窗/结晶波片消耗页确认 |
| `domain_farm_again.png` | 再次挑战按钮 |
| `domain_back_to_world.png` | 返回大世界按钮 |
| `mail_entry.png` | 终端菜单邮件入口 |
| `mail_claim_all.png` | 邮件一键领取按钮 |
| `battle_pass_entry.png` | 纪行入口 |
| `battle_pass_claim.png` | 纪行领取按钮 |
| `gray_confirm_exit_button.png` | 退出副本/确认弹窗，用于后续死亡恢复 |

首版暂未实现：梦魇声骸吸收、无音区、凝素领域、周常花园、死亡恢复。对应模板和路线后续补齐后再接入 C# 状态机。
