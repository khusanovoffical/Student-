# SchoolControl · Student (O‘quvchi mijoz dasturi)

Professional maktab kompyuterlarini boshqarish ekotizimining o‘quvchi kompyuterlariga o‘rnatiladigan rasmiy mijozi.

## Imkoniyatlari

- **Zero-Config Tarmoq Discovery:** O‘qituvchi serverini lokal tarmoqda UDP orqali avtomatik topadi va ulanadi.
- **Stable Device ID:** Har bir kompyuter uchun o‘zgarmas, doimiy apparat identifikatori (Device ID).
- **Yangi dars sessiyasi:** Har bir yangi darsda joriy o‘quvchining ism-familiyasi kiritiladi.
- **Ko‘rinadigan Ekran Sessiyasi:** Yashirin monitoring yo‘q! O‘qituvchi ekranni ko‘rganda yuqorida aniq va shaffof bo‘lmagan yashil xabar paydo bo‘ladi.
- **Lock / Unlock rejimi:** O‘qituvchi kompyuterni dars jarayoni uchun qulflaganda to‘liq ekranli xavfsiz qulf oynasi ko‘rsatiladi.
- **Fayllar almashinuvi:** O‘qituvchidan topshiriq va fayllarni qabul qilish hamda o‘qituvchiga bajarilgan vazifalarni yuborish.
- **Ilovalar nazorati:** Maktab administratori belgilagan taqiqlangan o‘yinlar va dasturlar avtomatik to‘siladi.

## Ishga tushirish va Build

```powershell
dotnet restore Student.csproj
dotnet build Student.csproj --configuration Release
dotnet publish Student.csproj --configuration Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Arxitektura va Xavfsizlik

- Dastur .NET 8 WPF asosida yaratilgan.
- SignalR real-time aloqa protokoli va avtomatik qayta ulanish (Auto-reconnect).
- Foydalanuvchi maxfiyligini hurmat qiladi, hech qanday yashirin josuslik yoki audit moduliga ega emas.
