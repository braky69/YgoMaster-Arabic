# YgoMaster-Arabic — دليل الاستخدام

نسخة معرّبة من [YgoMaster](https://github.com/pixeltris/YgoMaster). اللعبة لا تحتوي على خيار لغة عربية، لذلك يُكتب النص العربي فوق بيانات البطاقات الإنجليزية، مع نسخ احتياطي تلقائي للإنجليزية.

## التركيب

1. افتح مجلد اللعبة الأصلية، مثال:
   `C:\Program Files (x86)\Steam\steamapps\common\Yu-Gi-Oh! Master Duel\`
2. انسخ مجلد `YgoMaster` كاملاً (المجلد نفسه وليس محتوياته) إلى مجلد اللعبة، بحيث يصبح المسار:
   `...\Yu-Gi-Oh! Master Duel\YgoMaster\`
3. شغّل:
   `YgoMaster\YgoMasterLauncher.exe`

إذا ظهر تحذير من Windows SmartScreen اضغط **More info** ثم **Run anyway**.

> يجب أن تكون اللعبة منزّلة من Steam ومكتملة البيانات (أكمل الدرس التعليمي داخل اللعبة أولاً).

## تفعيل تعريب البطاقات

1. من واجهة المشغّل افتح تبويب **العربية (Arabic)**.
2. فعّل خيار **Enable Arabic card names and effects**.
3. اضغط **Apply**.
4. أغلق اللعبة والسيرفر، ثم أعد تشغيل اللعبة.

- النص العربي للعرض فقط: محرك اللعب ينفّذ التأثيرات الحقيقية كما هي، فاللعب يعمل بنفس الطريقة.
- لإرجاع الإنجليزية: أزل علامة الخيار ثم اضغط **Apply**. لا يمكن أن تتعطل اللعبة.
- يمكن اختيار خط عربي من القائمة داخل نفس التبويب.

## اللعب مع صديق (Host / Join)

- **Host:** افتح تبويب **Host (this PC)** → **Set up & start host** → اقبل صلاحية المسؤول (UAC). سيظهر عنوان مثل `192.168.1.20:4989`، أرسله لصديقك. اضغط **Allow firewall** مرة واحدة.
- **Join:** افتح تبويب **Join a friend** → اكتب عنوان الـ Host → **Set up & join game**.
- للعب خارج الشبكة المحلية (WAN): فعّل Port Forwarding في الراوتر للمنفذين **TCP 4989** و **TCP 4988** باتجاه جهاز الـ Host، وأرسل IP العام لصديقك.

## التحكم من داخل اللعبة

من القائمة الرئيسية افتح الـ Sub Menu (أيقونة الخطوط الثلاثة):

- YgoMaster: Host game
- YgoMaster: Join game
- YgoMaster: Settings
- YgoMaster: Open launcher

أي تغيير في الإعدادات يحتاج إعادة تشغيل اللعبة.

## تبويب اللاعب (الاسم / الجواهر / الغبار)

افتح تبويب **Player (Name / Gems / Dust)** واكتب اسم اللاعب وعدد الجواهر وغبار N/R/SR/UR ثم اضغط **Apply to player**.

- تُكتب القيم في `Data\Players\...\Player.json` وتُطبَّق عند تشغيل اللعبة.
- يجب أن تكون اللعبة والسيرفر مغلقين وقت التطبيق.
- الحد الأقصى للقيم 1,000,000,000.

## البطاقات والتأثيرات

الحزمة تفعّل كل بطاقة موجودة فعلياً في بيانات اللعبة المثبتة:
`UnlockAllCards`, `UnlockAllItems`, `UnlockAllSoloChapters`, `CardCraftableAll`, `DisableBanList`, `DisableDeckValidation`.

تأثيرات البطاقات ينفّذها محرك اللعبة نفسه (`duel.dll`). لا يمكن استيراد تأثيرات من ygoprodeck لأن Master Duel يستخدم محركه وقاعدة بطاقاته الخاصة؛ أي بطاقة غير موجودة في نسخة اللعبة لا يمكن إضافة تأثيرها.

## بناء حزمة التعريب من المصدر (للمطورين)

الأدوات موجودة في `tools/ArabScraper` (C# / .NET Framework):

1. **جمع البيانات:** شغّل `ArabScraper.exe <مجلد-العمل>`. سيجمع بيانات البطاقات من arab-duelists.com داخل `<مجلد-العمل>\cache` ويكتب `passcodes.txt`.
   - مجلد `arab-localization/cache` غير مرفق في المستودع لأنه بيانات خام كبيرة؛ أعد توليده بهذه الخطوة.
2. **توليد الحزمة:** شغّل `ArabGen.exe <مسار-المستودع>`.
   - يقرأ بيانات اللعبة الإنجليزية من `dist/stage/YgoMaster/Data/CardData/en-US` (غير مرفقة لأسباب حقوقية — استخرجها من نسختك من اللعبة).
   - يكتب الناتج في `arab-localization/out` (`CARD_Name.bytes`, `CARD_Desc.bytes`, `CARD_Indx.bytes`).
3. انسخ الناتج إلى `YgoMaster/Data/ArabicPack/ar` قبل بناء المشغّل، أو استخدم `Build.bat`.

> ملفات `out-test` والسجلات و`dist` و`obj` مستبعدة من Git لأنها مخرجات بناء/اختبار.

## حل المشاكل

- شغّل `YgoMasterLauncher.exe` كمسؤول (Run as administrator).
- تأكد أن المنفذين 4989 و 4988 غير مستخدمين من برنامج آخر.
- تأكد أن كل جهاز لديه `MultiplayerToken` مختلف (الأداة تولده تلقائياً).
- إذا لم يعمل اتصال LAN، جرّب إيقاف جدار الحماية مؤقتاً للتجربة.
- راجع [الملفات الأصلية للتوثيق](https://github.com/pixeltris/YgoMaster/tree/master/Docs) عند الحاجة.
