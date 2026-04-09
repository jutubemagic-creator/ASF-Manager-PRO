// Открытие/закрытие меню
document.querySelectorAll(".menu-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    const submenu = btn.nextElementSibling;

    submenu.style.display =
      submenu.style.display === "block" ? "none" : "block";
  });
});

// Переключение страниц
const content = document.getElementById("content");

document.querySelectorAll(".sub-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    const page = btn.dataset.page;

    if (page === "stats") {
      content.innerHTML = "<h1>📊 Статистика</h1><p>Здесь будет статистика</p>";
    }

    if (page === "reports") {
      content.innerHTML = "<h1>📑 Отчёты</h1><p>Здесь будут отчёты</p>";
    }

    if (page === "profile") {
      content.innerHTML = "<h1>👤 Профиль</h1><p>Настройки профиля</p>";
    }

    if (page === "security") {
      content.innerHTML = "<h1>🔒 Безопасность</h1><p>Настройки безопасности</p>";
    }
  });
});
