# HUST Network GUI

- 注意：毕业用不上校园网了，已停止维护。

**功能介绍**：自动认证HUST校园网，持续检测连接状态并尝试重连。

**拟解决的问题**：校园网经常出现断网弹出认证页面需要重新连接的情况(虽然个人感觉这种情况相对前几年变少了)，
特别是当你开着远程连接软件，放假回家后，此时工位上的电脑断网了，需要你重新认证？？！
以前的老锐捷客户端看似可以，实际用下来发现有些情况会弹窗连接失败让你点一下确定然后手动再点一下连接，问题是人不在电脑旁边点不了一点。 另外现在学校好像也淘汰了这个老客户端，完全转向网页204认证了。
Linux下用MentoHUST就没有这个问题，但是工位电脑一般是Windows，当长期离开学校需要远程办公时就需要想办法在断网后重新认证校园网。

**原理**：爬校园网认证页面的API。
