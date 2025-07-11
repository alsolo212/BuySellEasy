const categoryList = document.getElementById("category-list");

async function categoryShow() {
    const response = await fetch("/get-categories/list", {
        method: "GET"
    });

    categoryList.insertAdjacentHTML("beforeend", response.text);

}